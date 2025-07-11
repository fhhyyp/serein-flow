using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script
{
    public static class BinaryOperationEvaluator
    {
        public static object EvaluateValue(object left, string op, object right)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            // 特判字符串拼接
            if (op == "+" && (left is string || right is string))
            {
                return (left?.ToString() ?? "") + (right?.ToString() ?? "");
            }

            // 支持 null 运算（可按需扩展）
            if (left == null || right == null)
            {
                throw new InvalidOperationException($"无法对 null 执行操作：{op}");
            }

            // 尝试统一类型（浮点优先）
            var leftType = left.GetType();
            var rightType = right.GetType();

            Type resultType = GetWiderType(leftType, rightType);
            dynamic leftValue = Convert.ChangeType(left, resultType);
            dynamic rightValue = Convert.ChangeType(right, resultType);

            return op switch
            {
                "+" => leftValue + rightValue,
                "-" => leftValue - rightValue,
                "*" => leftValue * rightValue,
                "/" => rightValue == 0 ? throw new DivideByZeroException() : leftValue / rightValue,

                ">" => leftValue > rightValue,
                "<" => leftValue < rightValue,
                ">=" => leftValue >= rightValue,
                "<=" => leftValue <= rightValue,
                "==" => Equals(leftValue, rightValue),
                "!=" => !Equals(leftValue, rightValue),

                _ => throw new NotImplementedException($"未实现的操作符: {op}")
            };
        }




        /// <summary>
        /// 推导两个类型的“最通用类型”（类型提升）
        /// </summary>
        private static Type GetWiderType(Type t1, Type t2)
        {
            if (t1 == typeof(string) || t2 == typeof(string)) return typeof(string);
            if (t1 == typeof(decimal) || t2 == typeof(decimal)) return typeof(decimal);
            if (t1 == typeof(double) || t2 == typeof(double)) return typeof(double);
            if (t1 == typeof(float) || t2 == typeof(float)) return typeof(float);
            if (t1 == typeof(ulong) || t2 == typeof(ulong)) return typeof(ulong);
            if (t1 == typeof(long) || t2 == typeof(long)) return typeof(long);
            if (t1 == typeof(uint) || t2 == typeof(uint)) return typeof(uint);
            if (t1 == typeof(int) || t2 == typeof(int)) return typeof(int);

            // fallback
            return typeof(object);
        }















        public static Type EvaluateType(Type leftType, string op, Type rightType)
        {
            
            if (leftType == null || rightType == null)
                throw new ArgumentNullException("操作数类型不能为 null");


            // 字符串拼接
            if (op == "+" && (leftType == typeof(string) || rightType == typeof(string)))
                return typeof(string);

            // 比较操作总是返回 bool
            if (op is ">" or "<" or ">=" or "<=" or "==" or "!=")
                return typeof(bool);

            // 数值类型推导
            if (IsNumeric(leftType) && IsNumeric(rightType))
            {
                return PromoteNumericType(leftType, rightType);
            }

            // 逻辑操作
            if (op is "&&" or "||")
            {
                if (leftType == typeof(bool) && rightType == typeof(bool))
                    return typeof(bool);
                throw new InvalidOperationException($"逻辑操作 '{op}' 仅适用于 bool 类型");
            }

            throw new NotImplementedException($"不支持操作符 '{op}' 对 {leftType.Name} 和 {rightType.Name} 进行类型推导");
        }

        private static bool IsNumeric(Type type)
        {
            return type == typeof(byte) || type == typeof(sbyte) ||
                   type == typeof(short) || type == typeof(ushort) ||
                   type == typeof(int) || type == typeof(uint) ||
                   type == typeof(long) || type == typeof(ulong) ||
                   type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal);
        }

        // 提升到更高精度类型
        private static Type[] types = new[]
        {
            typeof(decimal),
            typeof(double),
            typeof(float),
            typeof(ulong),
            typeof(long),
            typeof(uint),
            typeof(int),
            typeof(ushort),
            typeof(short),
            typeof(byte),
            typeof(sbyte)
        };

        private static Type PromoteNumericType(Type left, Type right)
        {
            var ranks = types;
            foreach (var type in ranks)
            {
                if (type == left || type == right)
                    return type;
            }

            return typeof(object); // fallback
        }
    }





}


