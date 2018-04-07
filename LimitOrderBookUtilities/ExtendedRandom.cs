using System;
using System.Collections.Generic;
using System.Linq;

namespace LimitOrderBookUtilities
{   
    public class ExtendedRandom : Random
    {
        /// <inheritdoc />
        /// <summary>
        /// Construct randomizer from seed
        /// </summary>
        /// <param name="seed"></param>
        public ExtendedRandom(int seed) : base(seed) {}
        
        /// <inheritdoc />
        /// <summary>
        /// Start without seed
        /// </summary>
        public ExtendedRandom()
        {}
        
        /// <summary>
        /// Interarrival time of Poisson point process:  {X_n}
        /// are i.i.d with common distribution 
        /// </summary>
        /// <param name="lambda">Rate [1/time]</param>
        /// <returns></returns>
        public double NextExponentialTime(double lambda)
        {
            // Generate random number on [0,1]
            var u = NextDouble();
            // F(x) = P(X < x) = 1 - exp(- lambda * x)
            return -1.0 / lambda * Math.Log(1 - u);
        }

        /// <summary>
        /// Draw random action from given actions with relative probabilities  
        /// </summary>
        /// <param name="probabilities"></param>
        /// <returns></returns>
        public T NextFromProbabilities<T>(Dictionary<T, double> probabilities)
        {
            var r = NextDouble();
            // cumulative sum in C#
            var p = 0.0;
            foreach (var entry in probabilities)
            {
                var probability = entry.Value;
                var randomEvent = entry.Key;
                p += probability;
                if (r <= p)
                {
                    return randomEvent;
                }
            }
            // will never be reached as r el [0,1]
           return default(T) ;
        }
        
        /// <summary>
        /// Draw random action from given actions with relative probabilities  
        /// </summary>
        /// <param name="weights"></param>
        /// <returns></returns>
        public T NextFromWeights<T>(Dictionary<T, int> weights)
        {
            var r = NextDouble();
            // cumulative sum in C#
            var p = 0.0;
            var totalWeight = weights.Select(q=>q.Value).Sum();
            foreach (var entry in weights)
            {
                var probability = entry.Value / (double) totalWeight;
                var randomEvent = entry.Key;
                p += probability;
                if (r <= p)
                {
                    return randomEvent;
                }
            }
            // will never be reached as r el [0,1]
            return default(T) ;
        }
    }
}
