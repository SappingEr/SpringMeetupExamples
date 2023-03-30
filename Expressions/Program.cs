using System.Linq.Expressions;
using System.Text;

namespace Expressions;

class Program
{
	private static Dictionary<Type, string[]> _columnCache = new();

	public static void Main(string[] args)
	{
		#region Arithmetic expressions

		var x = 10;
		var y = 5;
		var z = x + y;
		var w = x * y;
		Console.WriteLine(z);
		Console.WriteLine(w);

		#endregion

		#region LogicalExpressions

		var a = true;
		var b = false;
		var c = a && b;
		var d = a || b;
		Console.WriteLine(c);
		Console.WriteLine(d);

		#endregion

		#region ConditionalExpressions

		var age = 18;
		var status = (age >= 18) ? "Adult" : "Minor";
		Console.WriteLine(status);

		#endregion

		#region Function calls

		var str = "hello";
		var len = str.Length;
		Console.WriteLine(len);

		#endregion

		#region Lambda

		var j = 10;
		var k = 5;
		Func<int, int, int> add = (l, r) => l + r;
		var result = add(j, k);
		Console.WriteLine(result); //15

		#endregion

		#region SimpleExpressionTree

		var lParameter = Expression.Parameter(typeof(int), "l");
		var rParameter = Expression.Parameter(typeof(int), "r");
		var addExpression = Expression.Add(lParameter, rParameter);

		var addFuncExpression =
			Expression.Lambda<Func<int, int, int>>(addExpression, lParameter, rParameter);

		Func<int, int, int> addFunc = addFuncExpression.Compile();

		var expressionResult = addFunc(j, k);
		Console.WriteLine(expressionResult); //15

		#endregion

		#region SimpleExpressionTreeVisitor

		Expression<Func<int, int>> expression = p => p * 3;

		var visitor = new MyVisitor();
		var visitedExpression = visitor.Visit(expression.Body);

		var visitedFunc = Expression.Lambda<Func<int, int>>(visitedExpression, expression.Parameters).Compile();
		var visitedFuncResult = visitedFunc(555);

		Console.WriteLine(visitedFuncResult);

		#endregion

		#region SqlBuilder

		Expression<Func<User, bool>> agePredicate = p => p.Age > 25;
		var sqlQuery = BuildSqlQuery(agePredicate);

		#endregion
	}

	class MyVisitor : ExpressionVisitor
	{
		public override Expression Visit(Expression? node)
		{
			Console.WriteLine(node.NodeType);
			return base.Visit(node);
		}
	}

	static string BuildSqlQuery<T>(Expression<Func<T, bool>> predicate)
	{
		if (predicate == null)
			throw new ArgumentNullException(nameof(predicate));

		var sqlQuery = new StringBuilder();
		var tableName = typeof(T).Name;
		var columns = GetColumns<T>();
		var visitor = new SqlQueryExpressionVisitor();

		visitor.Visit(predicate);
		var whereClause = $"WHERE {visitor.WhereClause}";
		sqlQuery.Append($"SELECT {string.Join(", ", columns)} FROM {tableName} {whereClause}");
		return sqlQuery.ToString();
	}

	static string[] GetColumns<T>()
	{
		var type = typeof(T);

		if (_columnCache.TryGetValue(type, out var columns))
			return columns;

		columns = type.GetProperties().Select(p => p.Name).ToArray();
		_columnCache[type] = columns;

		return columns;
	}

	class SqlQueryExpressionVisitor : ExpressionVisitor
	{
		private readonly StringBuilder _whereClauseBuilder = new();

		public string WhereClause => _whereClauseBuilder.ToString();

		protected override Expression VisitBinary(BinaryExpression node)
		{
			var left = Visit(node.Left);
			var right = Visit(node.Right);
			var sqlOperator = GetSqlOperator(node.NodeType);

			_whereClauseBuilder.Append($"{left.ToString().Split(".")[1]} {sqlOperator} {right}");
			return node;
		}

		private static string GetSqlOperator(ExpressionType expressionType)
		{
			return expressionType switch
			{
				ExpressionType.Equal => "=",
				ExpressionType.NotEqual => "!=",
				ExpressionType.GreaterThan => ">",
				ExpressionType.GreaterThanOrEqual => ">=",
				ExpressionType.LessThan => "<",
				ExpressionType.LessThanOrEqual => "<=",
				_ => throw new NotSupportedException($"Expression type '{expressionType}' is not supported.")
			};
		}
	}

	public class User
	{
		public int Id { get; set; }
		public string Name { get; set; } = null!;
		public int Age { get; set; }
	}
}