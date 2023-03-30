using BenchmarkDotNet.Running;
using CreatingObjects;

class Program
{
	public static void Main(string[] args)
	{
		var _ = BenchmarkRunner.Run<CreatingExamples>();
	}
}