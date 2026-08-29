using ScriptLang.Lexer;
using ScriptLang.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptLang.Parser
{
    internal static class ParserExtension
    {
        public static T SetToken<T>(this T expr, Token token) where T : Expr 
        {
            return expr;
        }
    }
}
