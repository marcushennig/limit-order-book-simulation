using System;
using System.Collections.Generic;

namespace LimitOrderBookUtilities
{

    public class RandomUtilities
    {
        #region Properties
        
        /// <summary>
        /// Random generator  
        /// </summary>
        private Random RandomGenerator { set; get; }

        #endregion Properties

        #region Constructor 

        /// <summary>
        /// Contructor
        /// </summary>
        public RandomUtilities(int seed)
        {
            RandomGenerator = new Random(seed);
        }

        #endregion Constructor 

        #region Methods

        /// <summary>
        /// Random integer in [a,b]
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public int Next(long a, long b)
        {
            return RandomGenerator.Next((int)a, (int)b);
        }

        /// <summary>
        /// Interarrival time of Poisson point process:  {X_n}
        /// are i.i.d with common distribution 
        /// </summary>
        /// <param name="lambda">Rate [1/time]</param>
        /// <returns></returns>
        public double PickExponentialTime(double lambda)
        {
            // Generate random number on [0,1]
            var u = RandomGenerator.NextDouble();
            // F(x) = P(X < x) = 1 - exp(- lambda * x)
            return -1.0 / lambda * Math.Log(1 - u);
        }

        /// <summary>
        /// Draw random action from given actions with relative probabilities  
        /// </summary>
        /// <param name="probabilities"></param>
        /// <returns></returns>
        public Action PickEvent(Dictionary<Action, double> probabilities)
        {
            var r = RandomGenerator.NextDouble();
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
            return null;
        }

        #endregion Methods
    }
}
