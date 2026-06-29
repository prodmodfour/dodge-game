using System;
using NUnit.Framework;
using ReactionTactics.Core;

namespace ReactionTactics.Tests.EditMode.Core
{
    public sealed class TacticalResultTests
    {
        [Test]
        public void SuccessResultHasNoErrorMessage()
        {
            var result = TacticalResult.Success();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.IsFailure, Is.False);
            Assert.That(result.ErrorMessage, Is.Empty);
            Assert.That(result.ToString(), Is.EqualTo("Success"));
        }

        [Test]
        public void FailureResultTrimsErrorMessageForUiAndLogs()
        {
            var result = TacticalResult.Failure("  Not enough AP.  ");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorMessage, Is.EqualTo("Not enough AP."));
            Assert.That(result.ToString(), Is.EqualTo("Failure: Not enough AP."));
        }

        [Test]
        public void FailureResultProvidesDefaultMessageWhenMessageIsBlank()
        {
            var result = TacticalResult.Failure("  ");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorMessage, Is.EqualTo("Command failed."));
        }

        [Test]
        public void GenericSuccessResultExposesValue()
        {
            var result = TacticalResult<int>.Success(7);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(7));
            Assert.That(result.ErrorMessage, Is.Empty);
        }

        [Test]
        public void GenericFailureResultCarriesErrorAndDoesNotExposeValue()
        {
            var result = TacticalResult<int>.Failure("Destination is blocked.");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorMessage, Is.EqualTo("Destination is blocked."));
            Assert.Throws<InvalidOperationException>(() => _ = result.Value);
        }

        [Test]
        public void GenericResultCanBeConvertedToNonGenericResult()
        {
            var success = TacticalResult<string>.Success("ok").WithoutValue();
            var failure = TacticalResult<string>.Failure("Out of range.").WithoutValue();

            Assert.That(success.IsSuccess, Is.True);
            Assert.That(failure.IsFailure, Is.True);
            Assert.That(failure.ErrorMessage, Is.EqualTo("Out of range."));
        }
    }
}
