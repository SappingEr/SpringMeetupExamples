using BenchmarkDotNet.Running;

namespace MappingExamples;

class Program
{
	// The project should be run in the release configuration.

	public static void Main(string[] args)
	{
		var _ = BenchmarkRunner.Run<Examples>();
	}
}