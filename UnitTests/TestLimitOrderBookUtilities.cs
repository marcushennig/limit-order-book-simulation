using System;
using System.Collections.Generic;
using System.Linq;
using LimitOrderBookUtilities;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class TestLimitOrderBookUtilities
    {        
        [Test]  
        public void TestSelectionOfRandomEvents()
        {
            var random = new RandomUtilities(34);
            var events = new List<Action>()
            {
                () => { },
                () => { },
                () => { },
                () => { },
            };
            
            var probability = new Dictionary<Action, double>
            {
                { events[0], 0.6},
                { events[1], 0.2},
                { events[2], 0.15},
                { events[3], 0.05},
            };
            var counter = new Dictionary<Action, double>
            {
                {events[0], 0},
                {events[1], 0},
                {events[2], 0},
                {events[3], 0},
            };
            const int n = 1000000;
            for (var i = 0; i < n; i++)
            {
                var e = random.PickEvent(probability);
                counter[e]++;
            }
            
            const double tolerance = 1e-3;
            var total = counter.Values.Sum();

            foreach (var e in events)
            {
                var measuredProbability = counter[e] / total;
                var expectedProbability = probability[e];
                
                Assert.True(Math.Abs(measuredProbability - expectedProbability) < tolerance, 
                    $"There is a deviation between expected {expectedProbability} " +
                    $"and measured probability {measuredProbability}");
            }
        }
    }
}