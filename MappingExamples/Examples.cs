using BenchmarkDotNet.Attributes;
using ExpressionsExamples.Models;
using FastMember;
using Microsoft.Data.SqlClient;
using Moq;
using System.Data;
using System.Linq.Expressions;

namespace MappingExamples
{
	[MemoryDiagnoser()]
	[HideColumns(
		BenchmarkDotNet.Columns.Column.Error,
		BenchmarkDotNet.Columns.Column.RatioSD,
		BenchmarkDotNet.Columns.Column.StdDev,
		BenchmarkDotNet.Columns.Column.AllocRatio,
		BenchmarkDotNet.Columns.Column.Gen0,
		BenchmarkDotNet.Columns.Column.Gen1,
		BenchmarkDotNet.Columns.Column.Gen2)]
	public class Examples
	{
		private const int Id = 10250;

		private const string ConnectionString =
			"data source = localhost;initial catalog = NorthwindTests; persist security info = True; Integrated Security = SSPI; Encrypt=false";

		private const string Query =
			"""
			 SELECT o.OrderID,
				    o.ShippedDate,
				    o.OrderDate,
				    o.ShipVia,
				    o.Freight,
				    o.ShipName,
				    o.ShipAddress,
				    o.ShipCity,
				    o.ShipRegion,
				    o.ShipPostalCode,
				    o.ShipCountry
			 FROM [dbo].[Orders] AS o
			 WHERE o.OrderID = @Id;
			""";

		private readonly Mock<IDataReader> _mockReader = GetMockDataReader();
		private readonly Func<IDataReader, Order> _cachedMap = Map<Order>();

		private IDataReader DbReader => _mockReader.Object;

		#region GetOrderByReader

		//4.754 us
		//[Benchmark(Baseline = true)]
		public Order? GetOrderByReader()
		{
			Order? order = null;
			var reader = DbReader;

			while (reader.Read())
			{
				order = ReadOrder(reader);
			}

			return order;
		}

		#endregion

		#region GetOrderByReflectionMapper

		//5.382 us
		//[Benchmark]
		public Order? GetOrderByReflectionMapper()
		{
			var reader = DbReader;
			var order = ReflectionMapObject<Order>(reader);
			return order;
		}

		#endregion

		#region GetOrderByReflectionMapperWithAccessor

		//5.093 us
		//[Benchmark]
		public Order? GetOrderByReflectionMapperWithAccessor()
		{
			var reader = DbReader;
			var order = ReflectionMapObjectTypeWithAccessor<Order>(reader);
			return order;
		}

		#endregion

		#region GetOrderByExpression

		//637.3 us
		//[Benchmark]
		public Order? GetOrderByExpression()
		{
			Order? order = null;
			var reader = DbReader;
			var mapper = Map<Order>();

			while (reader.Read())
			{
				order = mapper(reader);
			}

			return order;
		}

		#endregion

		#region GetOrderByCachedExpression

		//4.803 us
		//[Benchmark]
		public Order? GetOrderByCachedExpression()
		{
			Order? order = null;
			var reader = DbReader;

			while (reader.Read())
			{
				order = _cachedMap(reader);
			}

			return order;
		}

		#endregion

		#region GetOrderByReaderDB

		//117.3 us
		[Benchmark(Baseline = true)]
		public Order? GetOrderByReaderDb()
		{
			Order? order = null;
			using var connection = OpenConnection();
			var command = connection.CreateCommand();
			command.CommandType = CommandType.Text;
			command.CommandText = Query;
			command.Parameters.Add(new SqlParameter("@Id", Id));

			using var reader = command.ExecuteReader();

			if (!reader.HasRows)
				return order;

			while (reader.Read())
			{
				order = ReadOrder(reader);
			}

			return order;
		}

		#endregion

		#region GetOrderByReflectionMapperDb

		//126.4 us
		[Benchmark]
		public Order? GetOrderByReflectionMapperDb()
		{
			Order? order = null;
			using var connection = OpenConnection();
			var command = connection.CreateCommand();
			command.CommandType = CommandType.Text;
			command.CommandText = Query;
			command.Parameters.Add(new SqlParameter("@Id", Id));

			using var reader = command.ExecuteReader();

			if (!reader.HasRows)
				return order;

			order = ReflectionMapObject<Order>(reader);
			return order;
		}

		#endregion

		#region GetOrderByReflectionMapperWithAccessorDB

		//5.093 us
		[Benchmark]
		public Order? GetOrderByReflectionMapperWithAccessorDb()
		{
			Order? order = null;
			using var connection = OpenConnection();
			var command = connection.CreateCommand();
			command.CommandType = CommandType.Text;
			command.CommandText = Query;
			command.Parameters.Add(new SqlParameter("@Id", Id));

			using var reader = command.ExecuteReader();

			if (!reader.HasRows)
				return order;

			order = ReflectionMapObjectTypeWithAccessor<Order>(reader);
			return order;
		}

		#endregion GetOrderByReflectionMapperWithAccessorDB

		#region GetOrderByExpressionDb

		//1.008 ms
		[Benchmark]
		public Order? GetOrderByExpressionDb()
		{
			Order? order = null;
			using var connection = OpenConnection();
			var command = connection.CreateCommand();
			command.CommandType = CommandType.Text;
			command.CommandText = Query;
			command.Parameters.Add(new SqlParameter("@Id", Id));
			var mapper = Map<Order>();

			using var reader = command.ExecuteReader();

			if (!reader.HasRows)
				return order;

			while (reader.Read())
			{
				order = mapper(reader);
			}

			return order;
		}

		#endregion

		#region GetOrderByCachedExpressionDb

		//123.8 us
		[Benchmark]
		public Order? GetOrderByCachedExpressionDb()
		{
			Order? order = null;
			using var connection = OpenConnection();
			var command = connection.CreateCommand();
			command.CommandType = CommandType.Text;
			command.CommandText = Query;
			command.Parameters.Add(new SqlParameter("@Id", Id));
			var mapper = _cachedMap;

			using var reader = command.ExecuteReader();

			if (!reader.HasRows)
				return order;

			while (reader.Read())
			{
				order = mapper(reader);
			}

			return order;
		}

		#endregion

		private T? ReflectionMapObject<T>(IDataReader reader)
		{
			var type = typeof(T);
			var properties = type.GetProperties();
			var instance = Activator.CreateInstance(type);

			if (!reader.Read())
				return (T?)instance;

			for (var i = 0; i < reader.FieldCount; i++)
			{
				var fieldName = reader.GetName(i);
				var property =
					properties.FirstOrDefault(p
						=> string.Equals(p.Name, fieldName, StringComparison.OrdinalIgnoreCase));

				property?.SetValue(instance, reader.GetValue(i));
			}

			return (T?)instance;
		}

		private T? ReflectionMapObjectTypeWithAccessor<T>(IDataReader reader) where T : class, new()
		{
			var type = typeof(T);
			var accessor = TypeAccessor.Create(type);
			var members = accessor.GetMembers();
			var instance = new T();

			if (reader.Read())
			{
				for (var i = 0; i < reader.FieldCount; i++)
				{
					var fieldName = reader.GetName(i);
					var property =
						members.FirstOrDefault(m
							=> string.Equals(m.Name, fieldName, StringComparison.OrdinalIgnoreCase));

					if (property != null)
						accessor[instance, property.Name] = reader.GetValue(i);
				}
			}

			return (T?)instance;
		}

		private static Func<IDataReader, T> Map<T>()
		{
			var properties = typeof(T).GetProperties().ToArray();
			var readerParam = Expression.Parameter(typeof(IDataRecord), "reader");
			var bindings = new List<MemberBinding>();

			for (var i = 0; i < properties.Length; i++)
			{
				var property = properties[i];

				if (property.CanWrite)
				{
					var columnOrdinalExpression = Expression.Call(
						readerParam,
						"GetOrdinal", null,
						Expression.Constant(property.Name));
					var columnExpression = Expression.Call(
						readerParam, "GetValue", null, columnOrdinalExpression);
					var valueExpression = Expression.Convert(columnExpression, property.PropertyType);
					var assignmentExpression = Expression.Bind(property, valueExpression);
					bindings.Add(assignmentExpression);
				}
			}

			var body = Expression.MemberInit(Expression.New(typeof(T)), bindings);
			return Expression.Lambda<Func<IDataReader, T>>(body, readerParam).Compile();
		}

		private Order ReadOrder(IDataReader reader)
		{
			var i = 0;
			return new Order
			{
				OrderId = reader.GetInt32(i),
				ShippedDate = reader.GetDateTime(++i),
				OrderDate = reader.GetDateTime(++i),
				ShipVia = reader.GetInt32(++i),
				Freight = reader.GetDecimal(++i),
				ShipName = reader.GetString(++i),
				ShipAddress = reader.GetString(++i),
				ShipCity = reader.GetString(++i),
				ShipRegion = reader.GetString(++i),
				ShipPostalCode = reader.GetString(++i),
				ShipCountry = reader.GetString(++i)
			};
		}

		private static Mock<IDataReader> GetMockDataReader()
		{
			var order = new Order
			{
				OrderId = 183,
				OrderDate = DateTime.Now.Date,
				ShipVia = 2,
				Freight = 555,
				ShipName = "Acme Inc.",
				ShipAddress = "123 Main St",
				ShipCity = "Anytown",
				ShipRegion = "CA",
				ShipPostalCode = "12345",
				ShipCountry = "SomeCountry",
				ShippedDate = DateTime.Now.Date
			};

			var dataReaderMock = new Mock<IDataReader>();

			dataReaderMock.SetupSequence(r => r.Read())
				.Returns(true)
				.Returns(false);

			dataReaderMock.Setup(r => r.FieldCount).Returns(11);

			dataReaderMock.Setup(r => r.IsDBNull(0)).Returns(false);
			dataReaderMock.Setup(r => r.GetOrdinal(nameof(Order.OrderId))).Returns(0);
			dataReaderMock.Setup(r => r.GetName(0)).Returns(nameof(Order.OrderId));
			dataReaderMock.Setup(r => r.GetFieldType(0)).Returns(typeof(int));
			dataReaderMock.Setup(r => r.GetInt32(0)).Returns(order.OrderId);
			dataReaderMock.Setup(r => r.GetValue(0)).Returns(order.OrderId);

			dataReaderMock.Setup(r => r.IsDBNull(1)).Returns(false);
			dataReaderMock.Setup(r => r.GetOrdinal(nameof(Order.ShippedDate))).Returns(1);
			dataReaderMock.Setup(r => r.GetName(1)).Returns(nameof(Order.ShippedDate));
			dataReaderMock.Setup(r => r.GetFieldType(1)).Returns(typeof(DateTime?));
			dataReaderMock.Setup(r => r.GetDateTime(1)).Returns(order.ShippedDate.Value);
			dataReaderMock.Setup(r => r.GetValue(1)).Returns(order.ShippedDate);

			dataReaderMock.Setup(r => r.IsDBNull(2)).Returns(false);
			dataReaderMock.Setup(r => r.GetOrdinal(nameof(Order.OrderDate))).Returns(2);
			dataReaderMock.Setup(r => r.GetName(2)).Returns(nameof(Order.OrderDate));
			dataReaderMock.Setup(r => r.GetFieldType(2)).Returns(typeof(DateTime?));
			dataReaderMock.Setup(r => r.GetDateTime(2)).Returns(order.OrderDate.Value);
			dataReaderMock.Setup(r => r.GetValue(2)).Returns(order.OrderDate);

			dataReaderMock.Setup(r => r.IsDBNull(3)).Returns(false);
			dataReaderMock.Setup(r => r.GetOrdinal(nameof(Order.ShipVia))).Returns(3);
			dataReaderMock.Setup(r => r.GetName(3)).Returns(nameof(Order.ShipVia));
			dataReaderMock.Setup(r => r.GetFieldType(3)).Returns(typeof(int?));
			dataReaderMock.Setup(r => r.GetInt32(3)).Returns(order.ShipVia.Value);
			dataReaderMock.Setup(r => r.GetValue(3)).Returns(order.ShipVia);

			dataReaderMock.Setup(r => r.IsDBNull(4)).Returns(false);
			dataReaderMock.Setup(r => r.GetOrdinal(nameof(Order.Freight))).Returns(4);
			dataReaderMock.Setup(r => r.GetName(4)).Returns(nameof(Order.Freight));
			dataReaderMock.Setup(r => r.GetFieldType(4)).Returns(typeof(decimal?));
			dataReaderMock.Setup(r => r.GetDecimal(4)).Returns(order.Freight.Value);
			dataReaderMock.Setup(r => r.GetValue(4)).Returns(order.Freight);

			dataReaderMock.Setup(r => r.IsDBNull(5)).Returns(false);
			dataReaderMock.Setup(r => r.GetOrdinal(nameof(Order.ShipName))).Returns(5);
			dataReaderMock.Setup(r => r.GetName(5)).Returns(nameof(Order.ShipName));
			dataReaderMock.Setup(r => r.GetFieldType(5)).Returns(typeof(string));
			dataReaderMock.Setup(r => r.GetString(5)).Returns(order.ShipName);
			dataReaderMock.Setup(r => r.GetValue(5)).Returns(order.ShipName);

			dataReaderMock.Setup(r => r.IsDBNull(6)).Returns(false);
			dataReaderMock.Setup(r => r.GetOrdinal(nameof(Order.ShipAddress))).Returns(6);
			dataReaderMock.Setup(r => r.GetName(6)).Returns(nameof(Order.ShipAddress));
			dataReaderMock.Setup(r => r.GetFieldType(6)).Returns(typeof(string));
			dataReaderMock.Setup(r => r.GetString(6)).Returns(order.ShipAddress);
			dataReaderMock.Setup(r => r.GetValue(6)).Returns(order.ShipAddress);

			dataReaderMock.Setup(r => r.IsDBNull(7)).Returns(false);
			dataReaderMock.Setup(r => r.GetOrdinal(nameof(Order.ShipCity))).Returns(7);
			dataReaderMock.Setup(r => r.GetName(7)).Returns(nameof(Order.ShipCity));
			dataReaderMock.Setup(r => r.GetFieldType(7)).Returns(typeof(string));
			dataReaderMock.Setup(r => r.GetString(7)).Returns(order.ShipCity);
			dataReaderMock.Setup(r => r.GetValue(7)).Returns(order.ShipCity);

			dataReaderMock.Setup(r => r.IsDBNull(8)).Returns(false);
			dataReaderMock.Setup(r => r.GetOrdinal(nameof(Order.ShipRegion))).Returns(8);
			dataReaderMock.Setup(r => r.GetName(8)).Returns(nameof(Order.ShipRegion));
			dataReaderMock.Setup(r => r.GetFieldType(8)).Returns(typeof(string));
			dataReaderMock.Setup(r => r.GetString(8)).Returns(order.ShipRegion);
			dataReaderMock.Setup(r => r.GetValue(8)).Returns(order.ShipRegion);

			dataReaderMock.Setup(r => r.IsDBNull(9)).Returns(false);
			dataReaderMock.Setup(r => r.GetOrdinal(nameof(Order.ShipPostalCode))).Returns(9);
			dataReaderMock.Setup(r => r.GetName(9)).Returns(nameof(Order.ShipPostalCode));
			dataReaderMock.Setup(r => r.GetFieldType(9)).Returns(typeof(string));
			dataReaderMock.Setup(r => r.GetString(9)).Returns(order.ShipPostalCode);
			dataReaderMock.Setup(r => r.GetValue(9)).Returns(order.ShipPostalCode);

			dataReaderMock.Setup(r => r.IsDBNull(10)).Returns(false);
			dataReaderMock.Setup(r => r.GetOrdinal(nameof(Order.ShipCountry))).Returns(10);
			dataReaderMock.Setup(r => r.GetName(10)).Returns(nameof(Order.ShipCountry));
			dataReaderMock.Setup(r => r.GetFieldType(10)).Returns(typeof(string));
			dataReaderMock.Setup(r => r.GetString(10)).Returns(order.ShipCountry);
			dataReaderMock.Setup(r => r.GetValue(10)).Returns(order.ShipCountry);
			return dataReaderMock;
		}

		private static SqlConnection OpenConnection()
		{
			var connection = new SqlConnection(ConnectionString);
			connection.Open();
			return connection;
		}
	}
}