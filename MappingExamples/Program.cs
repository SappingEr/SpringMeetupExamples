﻿using BenchmarkDotNet.Running;

namespace MappingExamples;

class Program
{
	public static void Main(string[] args)
	{
		var _ = BenchmarkRunner.Run<Examples>();
	}
}