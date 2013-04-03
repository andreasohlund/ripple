using System;
using System.Collections.Generic;
using System.Linq;
using FubuCore;
using FubuCore.Descriptions;
using FubuCore.Logging;
using ripple.Commands;
using ripple.Model;
using ripple.Steps;

namespace ripple
{
	public class RippleOperation : DescribesItself, LogTopic
	{
		private readonly Solution _solution;
		private readonly SolutionInput _input;
		private readonly IRippleStepRunner _runner;
		private readonly IList<IRippleStep> _steps = new List<IRippleStep>();

		public RippleOperation(Solution solution, SolutionInput input, IRippleStepRunner runner)
		{
			_solution = solution;
			_input = input;
			_runner = runner;
		}

		public RippleOperation Step<T>()
			where T : IRippleStep, new()
		{
			Step(new T());
			return this;
		}

		public RippleOperation Step(IRippleStep step)
		{
			step.Solution = _solution;
			_steps.Add(step);

			return this;
		}

		public void Describe(Description description)
		{
			description.ShortDescription = _solution.Name;

			var list = description.AddList("RippleSteps", _steps);
			list.Label = "Ripple Steps";
			list.IsOrderDependent = true;
		}

		public bool Execute(bool throwOnFailure = false)
		{
			RippleLog.DebugMessage(this);

			foreach (var step in _steps)
			{
				try
				{
					step.Execute(_input, _runner);
				}
				catch (Exception ex)
				{
					cleanupPackages();
					_solution.Reset();

					RippleLog.Error("Error executing {0}".ToFormat(step.GetType().Name), ex);
					RippleLog.InfoMessage(_solution);
					
					// Mostly for testing
					if (throwOnFailure)
					{
						throw;
					}

					return false;
				}
			}

			_solution.EachProject(project => project.RemoveDuplicateReferences());
			_solution.Save();

			return true;
		}

		private void cleanupPackages()
		{
			var files = new FileSystem();
			var packages = new FileSet {Include = "*.nupkg", DeepSearch = false};

			var packageFiles = files.FindFiles(_solution.PackagesDirectory(), packages).ToArray();
			packageFiles.Each(file => files.DeleteFile(file));
		}

		public static RippleOperation For<T>(SolutionInput input)
		{
			return For<T>(input, Solution.For(input));
		}

		public static RippleOperation For<T>(SolutionInput input, Solution solution)
		{
			var description = input.DescribePlan(solution);
			if (description.IsNotEmpty())
			{
				RippleLog.Info(description);
			}

			input.Apply(solution);

			var runner = new RippleStepRunner(new FileSystem());
			return new RippleOperation(solution, input, runner).Step<ValidateRepository>();
		}
	}
}