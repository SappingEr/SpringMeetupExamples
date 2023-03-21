using System.Reflection.Emit;
using System.Reflection;


#region SimpleConsoleWriteLineMethodBuid

var assemblyBuilder =
	AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Assembly"), AssemblyBuilderAccess.Run);
var moduleBuilder = assemblyBuilder.DefineDynamicModule("Module");

// Define a new type
var typeBuilder = moduleBuilder.DefineType("SomeType", TypeAttributes.Public);

// Define a new method
var methodBuilder = typeBuilder.DefineMethod(
	"MyConsole",
	MethodAttributes.Public | MethodAttributes.Static, typeof(void),
	new Type[] { typeof(string) });

// Generate IL code for the method
var ilGenerator = methodBuilder.GetILGenerator();
ilGenerator.Emit(OpCodes.Ldarg_0);
ilGenerator.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new Type[] { typeof(string) })!);
ilGenerator.Emit(OpCodes.Ret);

// Create the type and call the new method
var type = typeBuilder.CreateType();
var method = type.GetMethod(name: "MyConsole");
method?.Invoke(null, new object[] { "Hello, world!" });

#endregion



// Simple mapper method call

var sourceObject = new SourceObject { Id = 345, Name = "SomeName" };
var mapper = Mapper<SourceObject, DestinationObject>.CreateMap();
var destinationObject = mapper(sourceObject);

Console.WriteLine($"Id: {destinationObject.Id}, Name: {destinationObject.Name}");

class Mapper<TSource, TDestination>
{
	public static Func<TSource, TDestination> CreateMap()
	{
		var sourceType = typeof(TSource);
		var destinationType = typeof(TDestination);

		var dynamicMethod = new DynamicMethod(
			"Map", destinationType, 
			new[] { sourceType }, true);

		var il = dynamicMethod.GetILGenerator();

		var destinationConstructor = destinationType.GetConstructor(Type.EmptyTypes);
		var destinationLocal = il.DeclareLocal(destinationType);

		il.Emit(OpCodes.Newobj, destinationConstructor!);
		il.Emit(OpCodes.Stloc, destinationLocal);

		foreach (var sourceProperty in sourceType.GetProperties())
		{
			var destinationProperty = destinationType.GetProperty(sourceProperty.Name);

			if (destinationProperty != null && destinationProperty.PropertyType == sourceProperty.PropertyType)
			{
				var sourceGetter = sourceProperty.GetGetMethod();
				var destinationSetter = destinationProperty.GetSetMethod();

				if (sourceGetter != null && destinationSetter != null)
				{
					il.Emit(OpCodes.Ldloc, destinationLocal);
					il.Emit(OpCodes.Ldarg_0);
					il.Emit(OpCodes.Callvirt, sourceGetter);
					il.Emit(OpCodes.Callvirt, destinationSetter);
				}
			}
		}

		il.Emit(OpCodes.Ldloc, destinationLocal);
		il.Emit(OpCodes.Ret);

		return (Func<TSource, TDestination>)dynamicMethod.CreateDelegate(typeof(Func<TSource, TDestination>));
	}
}

class SourceObject
{
	public int Id { get; set; }
	public string Name { get; set; } = null!;
}

class DestinationObject
{
	public int Id { get; set; }
	public string Name { get; set; } = null!;
}