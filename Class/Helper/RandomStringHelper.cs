using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjBobcat.Class.Helper
{
    public class RandomStringHelper
    {
        private const string Numbers = "0123456789";
        private const string LowerCases = "abcdefghijklmnopqrstuvwxyz";
        private const string UpperCases = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string Symbols = "~!@#$%^&*()_+=-`,./<>?;':[]{}\\|";

        private readonly List<char> _temp;

        public RandomStringHelper()
        {
            _temp = new List<char>();
        }

        public RandomStringHelper UseNumbers()
        {
            _temp.AddRange(Numbers.ToCharArray().ToList());
            return this;
        }

        public RandomStringHelper UseLower()
        {
            _temp.AddRange(LowerCases.ToCharArray().ToList());
            return this;
        }

        public RandomStringHelper UseUpper()
        {
            _temp.AddRange(UpperCases.ToCharArray().ToList());
            return this;
        }

        public RandomStringHelper UseSymbols()
        {
            _temp.AddRange(Symbols.ToCharArray().ToList());
            return this;
        }

        public RandomStringHelper HardMix(int times)
        {
            var range = Enumerable.Range(0, _temp.Count - 1).ToArray();
            for (var i = 0; i < times; i++)
            for (var j = 0; j < _temp.Count; j++)
            {
                var i1 = range.RandomSample();
                var i2 = range.RandomSample();

                while (i1 == i2) i2 = range.RandomSample();

                var temp = _temp[i1];
                _temp[i1] = _temp[i2];
                _temp[i2] = temp;
            }

            return this;
        }

        public string Generate(int length)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < length; i++) sb.Append(_temp.RandomSample());

            return sb.ToString();
        }
    }
}