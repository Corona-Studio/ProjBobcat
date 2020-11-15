using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjBobcat.Class.Helper
{
    /// <summary>
    ///     随机字符串帮助器。
    /// </summary>
    public class RandomStringHelper
    {
        /// <summary>
        ///     预设的数字字符构成的字符串。
        /// </summary>
        public const string Numbers = "0123456789";

        /// <summary>
        ///     预设的小写字母字符构成的字符串。
        /// </summary>
        public const string LowerCases = "abcdefghijklmnopqrstuvwxyz";

        /// <summary>
        ///     预设的大写字母字符构成的字符串。
        /// </summary>
        public const string UpperCases = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        /// <summary>
        ///     预设的标点字符构成的字符串。
        /// </summary>
        public const string Symbols = "~!@#$%^&*()_+=-`,./<>?;':[]{}\\|";

        private const int totalLength = 93;

        private readonly List<char> enabled;

        /// <summary>
        ///     创建一个新的随机字符串帮助器的实例。
        /// </summary>
        public RandomStringHelper()
        {
            enabled = new List<char>(totalLength);
        }

        /// <summary>
        ///     获取该帮助器已启用的字符所构成的字符串。
        ///     如果需要移除部分字符，请创建一个新的实例并重新添加。
        /// </summary>
        public string EnabledCharacters => new string(enabled.ToArray());

        /// <summary>
        ///     加入数字字符。
        ///     这个方法将使得 <seealso cref="Numbers" /> 中的字符被加入。
        ///     允许多次调用，如果出现多次调用，将提升此类字符的权重。
        /// </summary>
        /// <returns>帮助器本身。</returns>
        public RandomStringHelper UseNumbers()
        {
            enabled.AddRange(Numbers);
            return this;
        }

        /// <summary>
        ///     加入小写字母字符。
        ///     这个方法将使得 <seealso cref="LowerCases" /> 中的字符被加入。
        ///     允许多次调用，如果出现多次调用，将提升此类字符的权重。
        /// </summary>
        /// <returns>帮助器本身。</returns>
        public RandomStringHelper UseLower()
        {
            enabled.AddRange(LowerCases);
            return this;
        }

        /// <summary>
        ///     加入大写字母字符。
        ///     这个方法将使得 <seealso cref="UpperCases" /> 中的字符被加入。
        ///     允许多次调用，如果出现多次调用，将提升此类字符的权重。
        /// </summary>
        /// <returns>帮助器本身。</returns>
        public RandomStringHelper UseUpper()
        {
            enabled.AddRange(UpperCases);
            return this;
        }

        /// <summary>
        ///     加入标点字符。
        ///     这个方法将使得 <seealso cref="Symbols" /> 中的字符被加入。
        ///     允许多次调用，如果出现多次调用，将提升此类字符的权重。
        /// </summary>
        /// <returns>帮助器本身。</returns>
        public RandomStringHelper UseSymbols()
        {
            enabled.AddRange(Symbols);
            return this;
        }


        /// <summary>
        ///     加入一些字符。
        ///     这个方法将使得 <paramref name="characters" /> 中的元素被加入。
        ///     允许多次加入同一字符，这将提升此字符的权重。
        /// </summary>
        /// <param name="characters">要加入的字符。</param>
        /// <returns>帮助器本身。</returns>
        public RandomStringHelper UseCharacters(params char[] characters)
        {
            enabled.AddRange(characters);
            return this;
        }

        /// <summary>
        ///     加入一些字符。
        ///     这个方法将使得 <paramref name="characters" /> 中的元素被加入。
        ///     允许多次加入同一字符，这将提升此字符的权重。
        /// </summary>
        /// <param name="characters">要加入的字符。</param>
        /// <returns>帮助器本身。</returns>
        public RandomStringHelper UseCharacters(IEnumerable<char> characters)
        {
            enabled.AddRange(characters);
            return this;
        }

        /// <summary>
        ///     打乱顺序。此方法是不需要的。
        /// </summary>
        /// <param name="times">打乱次数。</param>
        /// <returns>帮助器本身。</returns>
        [Obsolete("不需要调用此方法。")]
        public RandomStringHelper HardMix(int times)
        {
            var range = Enumerable.Range(0, enabled.Count - 1).ToArray();
            for (var i = 0; i < times; i++)
            for (var j = 0; j < enabled.Count; j++)
            {
                var i1 = range.RandomSample();
                var i2 = range.RandomSample();

                while (i1 == i2) i2 = range.RandomSample();

                var temp = enabled[i1];
                enabled[i1] = enabled[i2];
                enabled[i2] = temp;
            }

            return this;
        }

        /// <summary>
        ///     随机打乱启用的元素
        /// </summary>
        /// <param name="times"></param>
        /// <returns></returns>
        public RandomStringHelper Shuffle(int times)
        {
            for (var i = 0; i < times; i++)
                enabled.Shuffle();

            return this;
        }

        /// <summary>
        ///     根据加入的字符生成一个新的随机字符串。
        ///     如果 <see cref="EnabledCharacters" /> 的长度为 0 ，将返回 null 。
        /// </summary>
        /// <param name="length">要生成的字符串的长度。</param>
        /// <returns>生成的字符串。</returns>
        public string Generate(int length)
        {
            if (enabled.Count == 0)
                return null;

            var sb = new StringBuilder(length);
            for (var i = 0; i < length; i++)
                sb.Append(enabled.RandomSample());

            return sb.ToString();
        }
    }
}