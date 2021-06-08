using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace SafePipeline.Tests
{
    public class PipelineTests
    {
        [Fact]
        public async Task The_pipeline_starts_correctly()
        {
            // arrange
            var startValue = "start value";

            // act
            var pipeline = await SafePipeline.StartWith(startValue);

            // assert
            pipeline.IsOk.Should().BeTrue();
            string result = pipeline;
            result.Should().Be("start value");
        }

        [Fact]
        public async Task The_pipeline_runs_in_the_correct_order()
        {
            // arrange
            var pipeline = SafePipeline.StartWith("start value");

            // act
            var result = await pipeline
                .Then(TestHelpers.AddStringValue)
                .Then(TestHelpers.WaitForIt)
                .Then(TestHelpers.AddStringValue);

            // assert
            result.IsOk.Should().BeTrue();
            result.Value.Should().Be("start value_Updated_Waited_Updated");
        }

        [Fact]
        public async Task The_pipeline_allows_logic_check_and_succeeds_when_condition_met()
        {
            // arrange
            var pipeline = SafePipeline.StartWith("yes");

            // act
            var result = await pipeline
                .Then(TestHelpers.YesNo)
                .Then(TestHelpers.AddStringValue)
                .Then(TestHelpers.WaitForIt);

            // assert
            result.IsOk.Should().BeTrue();
            result.Value.Should().Be("yes_Updated_Waited");
        }

        [Fact]
        public async Task The_pipeline_allows_logic_check_and_succeeds_when_condition_not_met()
        {
            // arrange
            var pipeline = SafePipeline.StartWith("start value");

            // act
            var result = await pipeline
                .Then(TestHelpers.AddStringValue)
                .Then(TestHelpers.YesNo)
                .Then(TestHelpers.WaitForIt);

            // assert
            result.IsOk.Should().BeTrue();
            result.Should().BeOfType<Skip<string>>();
        }

        [Fact]
        public async Task The_pipeline_converts_exceptions_to_failures()
        {
            // arrange
            var pipeline = SafePipeline.StartWith("start value");

            // act
            var result = await pipeline
                .Then(TestHelpers.ThrowNotImplemented);

            // assert
            result.IsOk.Should().BeFalse();
            result.Should().BeOfType<Fail<string>>();
            Exception ex = result;
            ex.Should().BeOfType<NotImplementedException>();
        }

        [Fact]
        public async Task The_pipeline_calls_onskip_when_skip_occurs()
        {
            // arrange
            var pipeline = SafePipeline.StartWith("NO");
            var monitor = new Monitor();

            // act
            var result = await pipeline
                .Then(TestHelpers.YesNo)
                .OnSuccess(s => monitor.Success = true)
                .OnFailure(s => monitor.Failure = true)
                .OnSkip(s => monitor.Skip = true);

            // assert
            result.IsOk.Should().BeTrue();
            result.Should().BeOfType<Skip<string>>();
            monitor.Should().BeEquivalentTo(new Monitor {Skip = true});
        }

        [Fact]
        public async Task The_pipeline_calls_onfailure_when_fail_occurs()
        {
            // arrange
            var pipeline = SafePipeline.StartWith("NO");
            var monitor = new Monitor();

            // act
            var result = await pipeline
                .Then(TestHelpers.ThrowNotImplemented)
                .OnSuccess(s => monitor.Success = true)
                .OnFailure(s => monitor.Failure = true)
                .OnSkip(s => monitor.Skip = true);

            // assert
            result.IsOk.Should().BeFalse();
            monitor.Should().BeEquivalentTo(new Monitor { Failure = true });
        }

        [Fact]
        public async Task The_pipeline_calls_onsuccess_when_all_ok()
        {
            // arrange
            var pipeline = SafePipeline.StartWith("YES");
            var monitor = new Monitor();

            // act
            var result = await pipeline
                .OnSuccess(s => monitor.Success = true)
                .OnFailure(s => monitor.Failure = true)
                .OnSkip(s => monitor.Skip = true);

            // assert
            result.IsOk.Should().BeTrue();
            result.Should().BeOfType<Ok<string>>();
            monitor.Should().BeEquivalentTo(new Monitor { Success = true });
        }

        [Fact]
        public void The_pipeline_works()
        {
            // arrange
            var pipeline = SafePipeline.StartWith("start value");
            var progress = "";

            // act
            Task<Operable<string>> task = pipeline
                .Then(TestHelpers.AddStringValue)
                .Then(TestHelpers.WaitAndThrow)
                .Then(TestHelpers.AddStringValue)
                .OnFailure(f =>
                {
                    progress = f.InputIntoFailedStep<string>();
                });
                
            Task.WaitAll(task);

            var result = task.Result;

            // assert
            result.IsOk.Should().BeFalse();
            result.Should().BeOfType<Fail<string>>();
            Exception ex = result;
            ex.Should().BeOfType<NotImplementedException>();

            progress.Should().Be("start value_Updated");
        }
    }
}
