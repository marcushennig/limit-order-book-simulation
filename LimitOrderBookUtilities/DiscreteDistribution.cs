using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;

namespace LimitOrderBookUtilities
{
    /// <summary>
    /// TODO: Need scaling functionality 
    /// TODO: Divide one distribution by other 
    /// Discrete probability distribution  
    /// </summary>
    public class DiscreteDistribution
    {
        #region Logging

        protected static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #endregion Logging

        #region Fields

        private SortedDictionary<double, double> _cdf;
        private SortedDictionary<double, double> _prob;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Underling data of distribution function
        /// </summary>
        public SortedDictionary<double, double> Data { private set; get; }

        /// <summary>
        /// Probability distribution 
        /// </summary>
        public SortedDictionary<double, double> Probability
        {
            get
            {
                if (_prob != null) return _prob;

                _prob = new SortedDictionary<double, double>();

                var totalWeight = Data.Sum(p => p.Value);

                foreach (var entry in Data)
                {
                    var x = entry.Key;
                    var p = ((double)entry.Value) / totalWeight;

                    Probability.Add(x, p);
                }
                return _prob;
            }
        }

        /// <summary>
        /// Cummulative distribution function
        /// </summary>
        public SortedDictionary<double, double> CummulativeDistributionFunction
        {
            get
            {
                if (_cdf != null) return _cdf;

                // Build cummulative dsitribution function 
                // from probability distribution function 
                _cdf = new SortedDictionary<double, double>();
                var sum = 0.0;
                foreach (var entry in Data)
                {
                    var x = entry.Key;
                    sum += entry.Value;
                    _cdf.Add(x, sum);
                }
                return _cdf;
            }
        }

        /// <summary>
        /// Total amount in dsitrbution
        /// </summary>
        public double TotalAmount { private set; get; }

        /// <summary>
        /// Mean value
        /// </summary>
        public double Mean => Moment(1);

        /// <summary>
        /// Variance
        /// </summary>
        public double Variance => Moment(2) - Mean * Mean;

        #endregion Properties

        #region Constructor 

        /// <summary>
        /// Constructor 
        /// </summary>
        public DiscreteDistribution(SortedDictionary<double, double> data)
        {
            if(data == null) throw new ArgumentException("Data cannot be null");

            Data = data;
            TotalAmount = data.Sum(p => p.Value);
        }

        /// <summary>
        /// Constructor 
        /// </summary>
        public DiscreteDistribution(IDictionary<long, long> data)
        {
            if (data == null) throw new ArgumentException("Data cannot be null");

            Data = new SortedDictionary<double, double>();
            TotalAmount = data.Sum(p => p.Value);
            foreach (var entry in data)
            {
                Data.Add(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Constructor 
        /// </summary>
        public DiscreteDistribution(IDictionary<long, double> data)
        {
            if (data == null) throw new ArgumentException("Data cannot be null");

            Data = new SortedDictionary<double, double>();
            TotalAmount = data.Sum(p => p.Value);
            foreach (var entry in data)
            {
                Data.Add(entry.Key, entry.Value);
            }
        }
        #endregion Constructor 

        #region Methods

        /// <summary>
        /// Moments of distibution 
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public double Moment(int n)
        {
            return Probability.Sum(p => Math.Pow(p.Key, n) * p.Value);
        }

        /// <summary>
        /// Determines quantile 
        /// </summary>
        /// <param name="q"></param>
        /// <returns></returns>
        public double Quantile(double q)
        {
            if (!CummulativeDistributionFunction.Any()) throw new Exception("The distribution is empty");

            if (q > 1) throw new ArgumentException("Q cannot be greater than 1");
            if (q < 0) throw new ArgumentException("Q cannot be smaller than 1");

            const double tolerance = 1e-12;

            if (Math.Abs(q) < tolerance) return CummulativeDistributionFunction.First().Key;
            if (Math.Abs(q - 1) < tolerance) return CummulativeDistributionFunction.Last().Key;

            var entry = CummulativeDistributionFunction.LastOrDefault(p => p.Value <= q * TotalAmount);

            return !entry.Equals(default(KeyValuePair<double, double>)) ? entry.Key : 0;
        }

        /// <summary>
        /// Save distribution 
        /// </summary>
        public void Save(string filePath)
        {
            try
            {
                using (var sw = new StreamWriter(filePath))
                {
                    foreach (var entry in Data)
                    {
                        sw.WriteLine($"{entry.Key}, {entry.Value}");
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error($"Exception: {exception}");
            }
        }

        /// <summary>
        /// Scale distribution (X, N)
        /// </summary>
        /// <param name="scalingX"></param>
        /// <param name="scalingN"></param>
        /// <returns></returns>
        public DiscreteDistribution Scale(double scalingX, double scalingN)
        {
            var scaledData = new SortedDictionary<double, double>();
            foreach (var entry in Data)
            {
                scaledData.Add(entry.Key *scalingX, entry.Value * scalingN);
            }
            return new DiscreteDistribution(scaledData);
        }

        /// <summary>
        /// Divide by another distribution 
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public DiscreteDistribution Divide(DiscreteDistribution other)
        {
            var data = new SortedDictionary<double, double>();
            foreach (var entry in Data)
            {
                var x = entry.Key;
                var v1 = entry.Value;

                if (other.Data.ContainsKey(x))
                {
                    var v2 = other.Data[x];

                    data.Add(x, v1 / v2);
                }
            }
            return new DiscreteDistribution(data);
        }

        #endregion Methods
    }
}