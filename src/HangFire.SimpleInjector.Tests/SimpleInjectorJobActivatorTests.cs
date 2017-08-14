namespace Hangfire.SimpleInjector.Tests
{
    using global::SimpleInjector;
    using System;
    using Xunit;

    public class SimpleInjectorJobActivatorTests
    {
        [Fact]
        public void CtorThrowsAnExceptionWhenContainerIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new SimpleInjectorJobActivator(null));
        }

        [Fact]
        public void ActivateJobCallsSimpleInjector()
        {
            var container = new Container();
            var theJob = new TestJob();
            container.RegisterSingleton<TestJob>(theJob);
            var activator = new SimpleInjectorJobActivator(container);
            var result = activator.ActivateJob(typeof(TestJob));
            Assert.Equal(theJob, result);
        }

    }
}