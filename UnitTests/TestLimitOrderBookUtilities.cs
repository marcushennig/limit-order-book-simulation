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
            var element = new List<string>{"A", "B", "C", "D"};
            
            var probability = new Dictionary<string, double>
            {
                { element[0], 0.6},
                { element[1], 0.2},
                { element[2], 0.15},
                { element[3], 0.05},
            };
            var counter = new Dictionary<string, double>
            {
                {element[0], 0},
                {element[1], 0},
                {element[2], 0},
                {element[3], 0},
            };
            const int n = 1000000;
            for (var i = 0; i < n; i++)
            {
                var e = random.NextFromProbabilities(probability);
                counter[e]++;
            }
            
            const double tolerance = 1e-3;
            var total = counter.Values.Sum();

            foreach (var e in element)
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