using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReactionTactics.Units;

namespace ReactionTactics.Tests.EditMode
{
    public sealed class TeamAndUnitIdentityTests
    {
        [Test]
        public void TeamHelpersIdentifyFriendlyAndHostileSides()
        {
            Assert.That(TeamId.Player.IsFriendlyTo(TeamId.Player), Is.True);
            Assert.That(TeamId.Enemy.IsFriendlyTo(TeamId.Enemy), Is.True);
            Assert.That(TeamId.Player.IsHostileTo(TeamId.Enemy), Is.True);
            Assert.That(TeamId.Enemy.IsHostileTo(TeamId.Player), Is.True);
        }

        [Test]
        public void TeamHelpersRejectUnknownTeams()
        {
            var unknown = (TeamId)999;

            Assert.Throws<ArgumentOutOfRangeException>(() => unknown.IsFriendlyTo(TeamId.Player));
            Assert.Throws<ArgumentOutOfRangeException>(() => TeamId.Player.IsHostileTo(unknown));
        }

        [Test]
        public void UnitIdSupportsEqualityHashingSortingAndLogging()
        {
            var first = new UnitId(1);
            var duplicateFirst = UnitId.FromInt(1);
            var second = new UnitId(2);
            var ids = new List<UnitId> { second, first };

            ids.Sort();

            Assert.That(first, Is.EqualTo(duplicateFirst));
            Assert.That(first == duplicateFirst, Is.True);
            Assert.That(first != second, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(duplicateFirst.GetHashCode()));
            Assert.That(ids, Is.EqualTo(new[] { first, second }));
            Assert.That(first.ToString(), Is.EqualTo("Unit#1"));
        }

        [Test]
        public void UnitIdNoneIsUnassignedAndSortsBeforeAssignedIds()
        {
            var none = UnitId.None;
            var assigned = new UnitId(1);

            Assert.That(none.IsAssigned, Is.False);
            Assert.That(assigned.IsAssigned, Is.True);
            Assert.That(none < assigned, Is.True);
            Assert.That(none.ToString(), Is.EqualTo("Unit#None"));
        }

        [Test]
        public void UnitIdRejectsNonPositiveAssignedValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new UnitId(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => UnitId.FromInt(-1));
        }

        [Test]
        public void UnitIdGeneratorReturnsDeterministicIncreasingIds()
        {
            var generator = new UnitIdGenerator(firstValue: 10);

            Assert.That(generator.Next(), Is.EqualTo(new UnitId(10)));
            Assert.That(generator.Next(), Is.EqualTo(new UnitId(11)));
            Assert.That(generator.Next(), Is.EqualTo(new UnitId(12)));
        }
    }
}
