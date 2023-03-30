using System.Linq.Expressions;
using System.Reflection.Emit;
using BenchmarkDotNet.Attributes;

namespace CreatingObjects;

[MemoryDiagnoser()]
[HideColumns(
	BenchmarkDotNet.Columns.Column.Error,
	BenchmarkDotNet.Columns.Column.RatioSD,
	BenchmarkDotNet.Columns.Column.StdDev,
	BenchmarkDotNet.Columns.Column.AllocRatio,
	BenchmarkDotNet.Columns.Column.Gen0,
	BenchmarkDotNet.Columns.Column.Gen1,
	BenchmarkDotNet.Columns.Column.Gen2)]
public class CreatingExamples
{
	private readonly Func<User> _userExpressionCreator = GetExpressionCreator<User>().Compile();
	private readonly Func<User> _userEmitCreator = GetEmitCreator<User>();

	[Benchmark(Baseline = true)]
	public User CreateUserByNewInstance()
	{
		return CreateNewInstance<User>();
	}

	[Benchmark]
	public User CreateUserByInvoke()
	{
		return CreateNewInstanceCtorInvoke<User>();
	}

	[Benchmark]
	public User? CreateUserByActivator()
	{
		return CreateNewInstanceActivator<User>();
	}

	[Benchmark]
	public User CreateUserByExpression()
	{
		return CreateNewInstanceExpression<User>();
	}

	[Benchmark]
	public User CreateUserByEmit()
	{
		return CreateNewInstanceEmit<User>();
	}

	[Benchmark]
	public User CreateUserByCachedExpression() => _userExpressionCreator();


	[Benchmark]
	public User CreateUserByCachedEmit()
	{
		return _userEmitCreator.Invoke();
	}

	T CreateNewInstance<T>() where T : new()
	{
		return new T();
	}

	T CreateNewInstanceCtorInvoke<T>()
	{
		var type = typeof(T);
		var ctor = type.GetConstructor(Type.EmptyTypes);
		return (T)ctor.Invoke(null);
	}

	T CreateNewInstanceActivator<T>()
	{
		var type = typeof(T);
		return (T)Activator.CreateInstance(type)!;
	}

	T CreateNewInstanceExpression<T>()
	{
		var create = GetExpressionCreator<T>().Compile();
		return create();
	}

	T CreateNewInstanceEmit<T>()
	{
		var creator = GetEmitCreator<T>();
		return creator.Invoke();
	}

	private static Expression<Func<T>> GetExpressionCreator<T>()
	{
		var type = typeof(T);
		var constructorExpression = Expression.New(type);
		return Expression.Lambda<Func<T>>(constructorExpression);
	}

	private static Func<T> GetEmitCreator<T>()
	{
		var type = typeof(T);
		var create = new DynamicMethod(
			$"NewInstance",
			type,
			null,
			typeof(CreatingExamples).Module,
			false);

		var ctor = type.GetConstructor(Type.EmptyTypes);

		var il = create.GetILGenerator();
		il.Emit(OpCodes.Newobj, ctor);
		il.Emit(OpCodes.Ret);

		return (Func<T>)create.CreateDelegate(typeof(Func<T>));
	}
}