using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    public class ArrayHelper
    {



        /// <summary>
        /// 数组尾部扩容
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="original"></param>
        /// <param name="length">扩容长度</param>
        /// <returns>新的数组</returns>
        /// <exception cref="Exception"> length 传入负值</exception>
        public static T[] Expansion<T>(T[] original, int length)
        {
            if(length == 0)
            {
                return original;
            }
            else if (length > 0)
            {
                // 创建一个新数组，比原数组大1
                T[] newArray = new T[original.Length + length];

                // 复制原数组的元素
                for (int i = 0; i < original.Length; i++)
                {
                    newArray[i] = original[i];
                }

                // 将新对象放在最后一位
                return newArray;
            }
            else
            {
                throw new Exception("不能减少数组长度");
            }
           
        }
        


        /// <summary>
        /// 为数组添加新的元素
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="original"></param>
        /// <param name="newObject"></param>
        /// <returns>新的数组</returns>
        public static T[] AddToArray<T>(T[] original, T newObject)
        {
            // 创建一个新数组，比原数组大1
            T[] newArray = ArrayHelper.Expansion(original, 1);

            original.CopyTo(newArray, 0);

            // 将新对象放在最后一位
            newArray[newArray.Length - 1] = newObject;
            return newArray;
        }

        /// <summary>
        /// 移除数组某个元素
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="original"></param>
        /// <param name="index"></param>
        /// <returns>新的数组</returns>
        public static T[] RemoteToArray<T>(T[] original, int index)
        {
            if (index == 0)
            {
                return new T[0];
            }
            // 创建一个新数组，比原数组小1
            T[] newArray = new T[original.Length - 1];

            for (int i = 0; i < index; i++)
            {
                newArray[i] = original[i];
            }
            for (int i = index; i < newArray.Length; i++)
            {
                newArray[i] = original[i + 1];
            }
            return newArray;
        }
    }
}
