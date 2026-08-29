using ScriptLang.Lexer;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ScriptLang.Parser;

/// <summary>
/// 语法分析器（Pratt Parser 实现）
/// </summary>
public class Parser(List<Token> tokens, string filePath)
{
    private readonly List<Token> _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    private int _current = 0;
    private readonly string _filePath = filePath;
    public Token CurrentToken => _tokens[_current];

    public List<ParseException> Diagnostics { get; } = [];



    /// <summary>
    /// 创建 SourceSpan 对象
    /// </summary>
    /// <param name="startIndex"></param>
    /// <param name="endIndex"></param>
    /// <returns></returns>
    public SourceSpan GetSourceSpan(int startIndex, int endIndex)
    {
        if (endIndex <= 0) endIndex = 1;
        Token start = _tokens[Math.Max(0, startIndex)];
        Token end = _tokens[Math.Min(_tokens.Count - 1, endIndex - 1)];

        //(Token start, Token end) = (_tokens[startIndex], _tokens[endIndex - 1]);
        return new SourceSpan(
            FilePath: start.FilePath ?? string.Empty,
            Start: start.StartIndex,
            Length: (end.StartIndex + end.Length) - start.StartIndex,
            StartLine: start.Line,
            StartColumn: start.Column ,
            EndLine: end.Line,
            EndColumn: end.Column + end.Length
        );
    }


    /// <summary>
    /// 解析整个程序
    /// </summary>
    public Expr Parse()
    {
        var statements = new List<Expr>();
        
        while (!IsAtEnd())
        {
            var statement = ParseDeclarationOrStatement();
            statements.Add(statement);
        }

        
        var sourceSpan = GetSourceSpan(0, _tokens.Count -1);

        return new ProgramExpr(statements, sourceSpan);
    }
    
    /// <summary>
    /// 解析声明或语句
    /// </summary>
    private Expr ParseDeclarationOrStatement()
    {
        try
        {
            if (Match(TokenType.Let))
                return ParseLetDeclaration();
            if (Match(TokenType.Var))
                return ParseVarDeclaration();
            if (Match(TokenType.Import))
            {
                if (Check(TokenType.LeftBrace))
                    return ParseImportDeclaration();
                Rollback(); // import( 是函数调用表达式，回退让 ParsePrimary 处理
            }
            if (Match(TokenType.Return))
                return ParseReturnStatement();

            return ParseExpression();
        }
        catch (ParseException ex)
        {
            Diagnostics.Add(ex);

            Synchronize();

            int start = Math.Max(_current - 1, 0);
            int end = Math.Min(_current + 1, _tokens.Count - 1);
            var span = GetSourceSpan(start, end);

            return GetErrorExpr(ex, span); 
        }
    }

    // ==================== 声明解析 ====================

    private ImportStmt ParseImportDeclaration()
    {
        // 解析 import { member1, member2 } from "filepath"
        Consume(TokenType.LeftBrace, "在 'import' 之后需要 '{'");
        var startTokenIndex = _current;
        var members = new List<(string member, string? alias)>();

        // 解析成员列表
        if (!Check(TokenType.RightBrace))
        {
            do
            {
                Token member = Consume(TokenType.Identifier, "在 import 列表中需要成员名称");
                Token? alias = null;
                if (Check(TokenType.Colon))
                {
                    Match(TokenType.Colon); // 消费别名定义 ':'
                    alias = Consume(TokenType.Identifier, "在 ':' 之后需要别名"); // 解析别名
                }
                members.Add((member.Lexeme, alias?.Lexeme));

            }
            while (Match(TokenType.Comma));
        }

        Consume(TokenType.RightBrace, "在 import 列表之后需要 '}'");
        Consume(TokenType.From, "在 import 成员之后需要 'from'");

        Token filePathToken = Consume(TokenType.String, "在 'from' 之后需要文件路径字符串");
        var endTokenIndex = _current;

        var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);

        return new ImportStmt(members, filePathToken.Lexeme, sourceSpan);
    }
    private LetExpr ParseLetDeclaration()
    {
        var startTokenIndex = _current;
        Token name = Consume(TokenType.Identifier, "在 'let' 之后需要变量名");
        Consume(TokenType.Equal, "在变量名之后需要 '='");
        Expr value = ParseExpression();

        var endTokenIndex = _current;
        var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
        return new LetExpr(name.Lexeme, value, sourceSpan);
    }

    private VarExpr ParseVarDeclaration()
    {
        var startTokenIndex = _current;
        Token name = Consume(TokenType.Identifier, "在 'var' 之后需要变量名");
        Consume(TokenType.Equal, "在变量名之后需要 '='");
        Expr value = ParseExpression();

        var endTokenIndex = _current;
        var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
        return new VarExpr(name.Lexeme, value, sourceSpan);
    }

    private ReturnExpr ParseReturnStatement()
    {
        if (PeekTypeHas(TokenType.Identifier, 
            TokenType.Null, 
            TokenType.Number_Int, TokenType.Number_Double,
            TokenType.String, TokenType.True, TokenType.Identifier, TokenType.False, 
            TokenType.LeftParen, 
            TokenType.LeftBracket,
            TokenType.LeftBrace
            ))
        {
            var startTokenIndex = _current;
            Expr value = ParseExpression();
            var endTokenIndex = _current;
            var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
            return new ReturnExpr(value, sourceSpan);
        }
        else if (PeekTypeHas(TokenType.LeftBracket) && IsNextLambda())
        {
            var startTokenIndex = _current;
            Expr value = ParseExpression();
            var endTokenIndex = _current;
            var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
            return new ReturnExpr(value, sourceSpan);
        }
        else
        {
            var startTokenIndex = _current;
            var endTokenIndex = _current;
            var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
            return new ReturnExpr(null, sourceSpan);
        }
    }


    // ==================== 表达式解析（Pratt Parser）====================
    /*
     解析顺序：
         ParseExpression
         └─ ParseAssignment
             └─ ParseOr
                 └─ ParseAnd
                     └─ ParseEquality
                         └─ ParseComparison
                             └─ ParseTerm
                                 └─ ParseFactor
                                     └─ ParseUnary
                                         └─ ParseCall
                                             └─ ParsePrimary
     */
    private Expr ParseExpression()
    {
        return ParseAssignment();
    }
    
    private Expr ParseAssignment()
    {
        var startTokenIndex = _current;
        Expr expr = ParseOr();

        if (Match(TokenType.Equal))
        {
            Token equals = Previous();
            Expr value = ParseAssignment();
            var endTokenIndex = _current;
            var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
            if (expr is IdentifierExpr identifier)
            {
                return new AssignExpr(identifier.Name, value, sourceSpan);
            }

            if (expr is IndexAccessExpr indexAccess)
            {
                return new IndexAssignExpr(indexAccess.Target, indexAccess.Index, value, sourceSpan);
            }

            if (expr is MemberAccessExpr memberAccess)
            {
                return new MemberAssignExpr(memberAccess.Target, memberAccess.Property, value, memberAccess.SafeNull, sourceSpan);
            }

            throw Error(equals, "无效的赋值目标");
        }

        return expr;
    }
    
    private Expr ParseOr()
    {
        var startTokenIndex = _current;
        Expr expr = ParseAnd();
        
        while (Match(TokenType.Or))
        {
            Token op = Previous();
            Expr right = ParseAnd();

            var endTokenIndex = _current;
            var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
            expr = new BinaryExpr(expr, op.Lexeme, right, sourceSpan);
        }
        
        return expr;
    }
    
    private Expr ParseAnd()
    {
        var startTokenIndex = _current;
        Expr expr = ParseEquality();
        
        while (Match(TokenType.And))
        {
            Token op = Previous();
            Expr right = ParseEquality();

            var endTokenIndex = _current;
            var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
            expr = new BinaryExpr(expr, op.Lexeme, right, sourceSpan);
        }
        
        return expr;
    }
    
    private Expr ParseEquality()
    {
        var startTokenIndex = _current;
        Expr expr = ParseComparison();
        
        while (Match(TokenType.EqualEqual, TokenType.BangEqual))
        {
            Token op = Previous();
            Expr right = ParseComparison();
            var endTokenIndex = _current;
            var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
            expr = new BinaryExpr(expr, op.Lexeme, right, sourceSpan);
        }
        
        return expr;
    }
    
    private Expr ParseComparison()
    {
        var startTokenIndex = _current;
        Expr expr = ParseTerm();
        
        while (Match(TokenType.Less, TokenType.LessEqual, TokenType.Greater, TokenType.GreaterEqual))
        {
            Token op = Previous();
            Expr right = ParseTerm();
            var endTokenIndex = _current;
            var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
            expr = new BinaryExpr(expr, op.Lexeme, right, sourceSpan);
        }
        
        return expr;
    }
    
    private Expr ParseTerm()
    {
        var startTokenIndex = _current;
        Expr expr = ParseFactor();
        
        while (Match(TokenType.Plus, TokenType.Minus))
        {
            Token op = Previous();
            Expr right = ParseFactor();
            var endTokenIndex = _current;
            var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
            expr = new BinaryExpr(expr, op.Lexeme, right, sourceSpan);
        }
        
        return expr;
    }
    
    private Expr ParseFactor()
    {
        var startTokenIndex = _current;
        Expr expr = ParseUnary();
        
        while (Match(TokenType.Star, TokenType.Slash, TokenType.Percent))
        {
            Token op = Previous();
            Expr right = ParseUnary();
            var endTokenIndex = _current;
            var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
            expr = new BinaryExpr(expr, op.Lexeme, right, sourceSpan);
        }
        
        return expr;
    }
    
    private Expr ParseUnary()
    {
        var startTokenIndex = _current;
        if (Match(TokenType.Bang, TokenType.Minus))
        {
            Token op = Previous();
            Expr right = ParseUnary();
            var endTokenIndex = _current;
            var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
            return new UnaryExpr(op.Lexeme, right, sourceSpan);
        }
        
        return ParseCall();
    }
    
    private Expr ParseCall()
    {
        // var startTokenIndex = _current;

        Expr expr = ParsePrimary();

        while (true)
        {
            if (Match(TokenType.LeftParen))
            {
                expr = ParseCallArguments(expr);
            }
            else if (Match(TokenType.LeftBracket))
            {
                expr = ParseIndexAccess(expr);
            }
            else if (Match(TokenType.Dot, TokenType.QuestionDot))
            {
                Token op = Previous();
                var startTokenIndex = _current ;
                Token name = Consume(TokenType.Identifier, "在 '.' 之后需要属性名");
                bool safeNull = op.Type == TokenType.QuestionDot;

                var endTokenIndex = _current;
                var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
                expr = new MemberAccessExpr(expr, name.Lexeme, safeNull, sourceSpan);
            }
            else
            {
                break;
            }
        }
        
        return expr;
    }
    
    private CallExpr ParseCallArguments(Expr target)
    {
        var args = new List<Expr>();
        var startTokenIndex = _current;

        if (!Check(TokenType.RightParen))
        {
            do
            {
                try
                {
                    args.Add(ParseExpression());
                }
                catch (ParseException ex)
                {
                    Diagnostics.Add(ex);
                    var itemSpan = GetSourceSpan(_current, _current + 1);
                    var ee = GetErrorExpr(ex, itemSpan);
                    args.Add(ee);
                    Synchronize();
                }
            }
            while (Match(TokenType.Comma));
        }

        Consume(TokenType.RightParen, "在参数之后需要 ')'");

        var endTokenIndex = _current;
        var span = GetSourceSpan(startTokenIndex, endTokenIndex);
        return new CallExpr(target, args, span);
    }
    
    private IndexAccessExpr ParseIndexAccess(Expr target)
    {
        var startTokenIndex = _current;
        Expr index = ParseExpression();
        Consume(TokenType.RightBracket, "在索引之后需要 ']'");
        var endTokenIndex = _current;
        var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
        return new IndexAccessExpr(target, index, sourceSpan);
    }
    
    private Expr ParsePrimary()
    {
        var startTokenIndex = _current;
        // 字面量
        if (Match(TokenType.Number_Int, TokenType.Number_Long, 
            TokenType.Number_Float, TokenType.Number_Double, TokenType.Number_Decimal,
            TokenType.String, TokenType.True, TokenType.False, TokenType.Null))
        {
            var endTokenIndex = _current;
            var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
            return new LiteralExpr(Previous().Literal, sourceSpan);
        }
        // 括号 / Lambda
        if (Match(TokenType.LeftParen))
        {
            if (IsNextLambda())
            {
                return ParseLambda();
            }
            else
            {
                // 普通括号表达式
                Expr expr = ParseExpression();
                Consume(TokenType.RightParen, "在表达式之后需要 ')'");
                return expr;
            }
        }

        // 标识符 / 单参数Lambda

        if (Match(TokenType.Identifier))
        {
            if (Peek().Type == TokenType.Arrow)
            {
                Expr expr = ParseLambda();
                return expr;
            }
            else
            {
                var endTokenIndex = _current;
                var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
                return new IdentifierExpr(Previous().Lexeme, sourceSpan);
            }
        }

        // import("path") 动态加载 — 在表达式中当标识符处理
        if (Match(TokenType.Import))
        {
            var endTokenIndex = _current;
            var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
            return new IdentifierExpr("import", sourceSpan);
        }
        
        // If-Then-Else 表达式
        if (Match(TokenType.If))
        {
            return ParseIfExpression();
        }
        
        // When 表达式
        if (Match(TokenType.When))
        {
            return ParseWhenExpression();
        }
        
        // For 循环表达式
        if (Match(TokenType.For))
        {
            return ParseForExpression();
        }
        
        // 对象字面量
        if (Match(TokenType.LeftBrace))
        {
            return ParseObjectLiteral();
        }
        
        // 数组字面量
        if (Match(TokenType.LeftBracket))
        {
            return ParseArrayLiteral();
        }

        //throw Error(Peek(), "Expect expression");
        // 代替异常
        var token = Peek();
        var error = Error(token, "需要表达式");
        Diagnostics.Add(error);

        Advance(); // 吃掉一个 token，防止死循环

        var span = GetSourceSpan(
            Math.Max(_current - 1, 0),
            Math.Min(_current + 1, _tokens.Count - 1)
        );

        return GetErrorExpr(error, span);  
    }

    // ==================== 复杂表达式解析 ====================
    /// <summary>
    /// 判断当前 '(' 是否可能是 Lambda
    /// </summary>
    private bool IsNextLambda()
    {
        int save = _current;

        try
        {
            // 空参数: () =>
            if (Check(TokenType.RightParen))
            {
                Advance(); // 消费 ')'
                bool isLambda = Check(TokenType.Arrow);
                _current = save;
                return isLambda;
            }

            // 单参数或多参数
            bool firstIsIdentifier = Check(TokenType.Identifier);
            if (!firstIsIdentifier) return false;

            Advance(); // 消费第一个标识符

            if (Check(TokenType.Comma))
            {
                // 多参数情况，至少一个逗号
                while (Check(TokenType.Comma))
                {
                    Advance(); // 消费 ','
                    if (!Check(TokenType.Identifier))
                    {
                        _current = save;
                        return false; // 不是合法参数
                    }
                    Advance(); // 消费参数
                }
            }

            if (!Check(TokenType.RightParen))
            {
                _current = save ;
                return false;
            }


            Advance(); // 消费 ')'
            bool isArrow = Check(TokenType.Arrow);

            _current = save;
            return isArrow;
        }
        catch
        {
            _current = save;
            return false;
        }
    }

    /// <summary>
    /// 解析Lambda（闭包实现）表达式
    /// </summary>
    /// <returns></returns>
    private LambdaExpr ParseLambda()
    {
        List<string> parameters = [];

        var startTokenIndex = _current;
        // ParseLambda 被调用时，可能是在 ( 之后，或者是在标识符之后
        // 我们需要判断当前状态

        if (Check(TokenType.Arrow))
        {
            // 已经是Lambda符号了，前面可能是 单个 Identifier 
            Rollback();
        }

        if (Check(TokenType.Identifier))
        {
            // 可能是单参数: a => ...
            // 也可能是多参数的第一个: (a, b) => ...
            // 需要向前看

            if (Match(TokenType.Identifier))
            {
                parameters.Add(Previous().Lexeme);

                if (Match(TokenType.Comma))
                {
                    // 多参数，继续读取
                    do
                    {
                        parameters.Add(Consume(TokenType.Identifier, "需要参数名").Lexeme);
                    } while (Match(TokenType.Comma));

                    Consume(TokenType.RightParen, "在参数之后需要 ')'");
                }
                else if (Check(TokenType.RightParen))
                {
                    // 单参数，需要消费 )
                    Consume(TokenType.RightParen, "在参数之后需要 ')'");
                }

                Consume(TokenType.Arrow, "在参数之后需要 '=>'");
            }
        }
        else if (Check(TokenType.RightParen))
        {
            // 空参数: () => expr
            Consume(TokenType.RightParen, "Lambda 表达式需要 ')'");
            Consume(TokenType.Arrow, "在参数之后需要 '=>'");
        }
        else
        {
            throw Error(Peek(), "Lambda 表达式需要标识符或 ')'");
        }

        Expr body;
        // 检查 body 是否是 block
        if (Check(TokenType.LeftBrace))
        {
            body = ParseBlock();
        }
        else
        {
            body = ParseExpression();
        }
        var endTokenIndex = _current;
        var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
        return new LambdaExpr(parameters, body, sourceSpan);
    }

    /// <summary>
    /// 解析循环语句
    /// </summary>
    /// <returns></returns>
    private IfExpr ParseIfExpression()
    {
        var startTokenIndex = _current;
        Expr condition = ParseExpression();
        
        Expr thenBranch;
        if (Match(TokenType.Then))
        {
            // 检查后面是否是 { 开头的 block
            if (Check(TokenType.LeftBrace))
            {
                thenBranch = ParseBlock();
            }
            else
            {
                thenBranch = ParseExpression();
            }
        }
        else
        {
            // 允许省略 then，直接使用表达式或 block
            if (Check(TokenType.LeftBrace))
            {
                thenBranch = ParseBlock();
            }
            else
            {
                thenBranch = ParseExpression();
            }
        }

        var endTokenIndex = _current;
        var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
        Expr elseBranch = new LiteralExpr(null, sourceSpan);
        if (Match(TokenType.Else))
        {
            // 检查后面是否是 { 开头的 block
            if (Check(TokenType.LeftBrace))
            {
                elseBranch = ParseBlock();
            }
            else
            {
                elseBranch = ParseExpression();
            }
        }

        var endTokenIndex_if = _current;
        var sourceSpan_if = GetSourceSpan(startTokenIndex, endTokenIndex_if);
        return new IfExpr(condition, thenBranch, elseBranch, sourceSpan_if);
    }

    /// <summary>
    /// 解析代码块 { ... }
    /// </summary>
    /// <returns></returns>
    private BlockExpr ParseBlock()
    {
        var startTokenIndex = _current;
        Consume(TokenType.LeftBrace, "需要 '{' 开始代码块");

        var statements = new List<Expr>();

        while (!Check(TokenType.RightBrace) && !IsAtEnd())
        {
            statements.Add(ParseDeclarationOrStatement());
        }

        Consume(TokenType.RightBrace, "需要 '}' 结束代码块");

        var endTokenIndex = _current;
        var span = GetSourceSpan(startTokenIndex, endTokenIndex);
        return new BlockExpr(statements, span);

    }

    /// <summary>
    /// 解析模式匹配
    /// </summary>
    /// <returns></returns>
    private WhenExpr ParseWhenExpression()
    {
        var startTokenIndex = _current;
        // 先解析 when 的值表达式
        Expr value = ParseExpression();
        var clauses = new List<WhenClause>();
        WhenClause? otherClause = null;
        // 如果是大括号 { ... } 包裹的多子句形式
        if (Match(TokenType.LeftBrace))
        {
            while (!Check(TokenType.RightBrace) && !IsAtEnd())
            {
                //var t = _currentToken;
                var startTokenIndex_when = _current;
                // 解析模式表达式
                Expr pattern = ParseExpression();
                if(pattern is LambdaExpr lambda)
                {
                    var endTokenIndex_when = _current;
                    var sourceSpan_when = GetSourceSpan(startTokenIndex_when, endTokenIndex_when);
                    otherClause = new WhenClause(null!, lambda, sourceSpan_when);
                    break;
                }
                else
                {
                    Consume(TokenType.Arrow, "在 when 子句中需要 '=>'");

                    // 支持块语句作为 when 子句体
                    Expr body;
                    if (Check(TokenType.LeftBrace))
                    {
                        body = ParseBlock(); // 解析 { ... } 块语句
                    }
                    else
                    {
                        body = ParseExpression();
                    }



                    var endTokenIndex_when = _current;
                    var sourceSpan_when = GetSourceSpan(startTokenIndex_when, endTokenIndex_when);
                    var clase = new WhenClause(pattern, body, sourceSpan_when);
                    clauses.Add(clase);

                    // 检查逗号分隔符
                    if (!Match(TokenType.Comma))
                    {
                        if (!Check(TokenType.RightBrace))
                        {
                            throw Error(Peek(), "在 when 子句之后需要 ',' 或 '}'");
                        }
                    }
                }

               
            }

            Consume(TokenType.RightBrace, "在 when 子句之后需要 '}'");
        }
        /*else
        {
            // 单行形式 when 循环解析
            while (!Check(TokenType.Else) && !Check(TokenType.EOF) && !Check(TokenType.Semicolon))
            {
                var startTokenIndex_when = _current;

                Expr pattern = ParseExpression();
                Consume(TokenType.Arrow, "在 when 子句中需要 '=>'");

                Expr body;
                if (Check(TokenType.LeftBrace))
                {
                    body = ParseBlock();
                }
                else
                {
                    body = ParseExpression();
                }

                var endTokenIndex_when = _current;
                var sourceSpan_when = GetSourceSpan(startTokenIndex_when, endTokenIndex_when);
                clauses.Add(new WhenClause(pattern, body, sourceSpan_when));

                if (!Match(TokenType.Comma))
                    break;
            }
        }*/

        var endTokenIndex = _current;
        var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
        return new WhenExpr(value, clauses, otherClause, sourceSpan);
    }

    /// <summary>
    /// 解析循环
    /// </summary>
    /// <returns></returns>
    private ForExpr ParseForExpression()
    {
        var startTokenIndex = _current;
        string varName = Consume(TokenType.Identifier, "在 'for' 之后需要变量名").Lexeme;
        Consume(TokenType.In, "在变量名之后需要 'in'");
        Expr iterable = ParseExpression();
        Expr body;

        // 检查后面是否是 { 开头的 block
        if (Check(TokenType.LeftBrace))
        {
            body = ParseBlock();
        }
        else
        {
            body = ParseExpression();
        }

        var endTokenIndex = _current;
        var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
        return new ForExpr(varName, iterable, body, sourceSpan);
    }

    /// <summary>
    /// 解析对象定义
    /// </summary>
    /// <returns></returns>
    private ObjectLiteralExpr ParseObjectLiteral()
    {
        var startTokenIndex = _current;
        var properties = new List<ObjectProperty>();

        if (!Check(TokenType.RightBrace))
        {
            do
            {

                var startTokenIndex_obj = _current;
                string key;
                if (Check(TokenType.RightBrace))
                {
                    break;
                }
                if (Check(TokenType.Identifier))
                {
                    key = Advance().Lexeme;
                }
                else if (Check(TokenType.String))
                {
                    key = (string)Advance().Literal!;
                }
                else
                {
                    throw Error(Peek(), "需要属性键");
                }

                if (Match(TokenType.Equal))
                {
                    Expr value = ParseExpression();
                    var endTokenIndex_obj = _current;
                    var sourceSpan_obj = GetSourceSpan(startTokenIndex_obj, endTokenIndex_obj);
                    properties.Add(new ObjectProperty(key, value, sourceSpan_obj));
                }
                else
                {
                    // 简写形式: { a } 等价于 { a = a }

                    var endTokenIndex_obj = _current;
                    var sourceSpan_obj = GetSourceSpan(startTokenIndex_obj, endTokenIndex_obj);

                    var sourceSpan_ide = GetSourceSpan(startTokenIndex_obj, endTokenIndex_obj);
                    var ide = new IdentifierExpr(key, sourceSpan_ide);
                    properties.Add(new ObjectProperty(key, ide, sourceSpan_obj));
                }
            } while (Match(TokenType.Comma));
        }

        Consume(TokenType.RightBrace, "在对象属性之后需要 '}'");

        var endTokenIndex = _current;
        var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
        return new ObjectLiteralExpr(properties, sourceSpan);
    }

    /// <summary>
    /// 解析数组定义
    /// </summary>
    /// <returns></returns>
    private ArrayLiteralExpr ParseArrayLiteral()
    {

        var startTokenIndex = _current;
        var elements = new List<Expr>();

        if (!Check(TokenType.RightBracket))
        {
            do
            {

                if (Check(TokenType.RightBracket))
                {
                    break;
                }
                elements.Add(ParseExpression());

            } while (Match(TokenType.Comma));
        }

        Consume(TokenType.RightBracket, "在数组元素之后需要 ']'");
        var endTokenIndex = _current;
        var sourceSpan = GetSourceSpan(startTokenIndex, endTokenIndex);
        return new ArrayLiteralExpr(elements, sourceSpan);
    }

    // ==================== 辅助方法 ====================

    /// <summary>
    /// 检查是否是指定类型的Token，如果是则消耗并自动获取下一个Token，返回true。如果不是则返回false。
    /// </summary>
    /// <param name="types"></param>
    /// <returns></returns>
    private bool Match(params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 判断某个token是否为某个类型
    /// </summary>
    /// <param name="token"></param>
    /// <param name="types"></param>
    /// <returns></returns>
    private static bool MatchFor(TokenType tokenType, params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (tokenType == type)
            {
                return true;
            }
        }
        return false;
    }
    
    private bool PeekTypeHas(params TokenType[] types)
    {
        if (IsAtEnd()) return false;
        var peekType = Peek().Type;
        return types.Any(x =>  x == peekType);
    }
    
    /// <summary>
    /// 检查当前Token是否为目标类型
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private bool Check(TokenType type)
    {
        if (IsAtEnd()) return false;
        var peek = Peek();
        return peek.Type == type;
    }

    /// <summary>
    /// 回退一个索引
    /// </summary>
    private Token Rollback()
    {
        _current--;
        return Peek();
    }
    /// <summary>
    /// 获取下一个Token
    /// </summary>
    /// <returns></returns>
    private Token Advance()
    {
        if (!IsAtEnd()) _current++;
        return Previous(); 
    }
    
    /// <summary>
    /// 判断是否已经结束
    /// </summary>
    /// <returns></returns>
    private bool IsAtEnd()
    {
        return Peek().Type == TokenType.EOF;
    }
    
    /// <summary>
    /// 看一下当前Token
    /// </summary>
    /// <param name="offset"></param>
    /// <returns></returns>
    private Token Peek()
    {
        return _tokens[_current];
    }
    
    /// <summary>
    /// 回看上一个Token
    /// </summary>
    /// <returns></returns>
    private Token Previous()
    {
        return _tokens[_current - 1];
    }
    

    /// <summary>
    /// 检查是否为指定类型，如果是则消耗当前Tokoen，如果不是则抛出异常
    /// </summary>
    /// <param name="type"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    private Token Consume(TokenType type, string message)
    {
        if (Check(type))
            return Advance();

        // 1. 生成并记录 diagnostic
        var error = Error(Peek(), message);
        Diagnostics.Add(error);

        // 2. 尝试恢复：如果当前 token 看起来是“同步点”，不吃
        if (IsRecoverableBoundary(Peek().Type))
            return Peek();

        // 3. 否则吃掉一个 token，避免死循环
        Advance();

        // 4. 返回一个“占位 token”
        return Previous();
    }

    private static bool IsRecoverableBoundary(TokenType type)
    {
        return type switch
        {
            TokenType.Semicolon => true,
            TokenType.RightParen => true,
            TokenType.RightBrace => true,
            TokenType.RightBracket => true,
            TokenType.Comma => true,
            TokenType.EOF => true,
            _ => false
        };
    }

    /// <summary>
    /// 错误提示
    /// </summary>
    /// <param name="token"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    private static  ParseException Error(Token token, string message)
    {
        string location = $"[文件 {token.FilePath} 第 {token.Line} 行, 第 {token.Column} 列]";
        string tokenInfo = $"找到 '{token.Lexeme}' (类型: {token.Type})";
        string fullMessage = $"{message}。 {tokenInfo} {location} ";
        return new ParseException(token, fullMessage);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ErrorExpr GetErrorExpr(Exception exception, SourceSpan sourceSpan)
    {
        return new ErrorExpr(exception.Message, sourceSpan);
    }

    /// <summary>
    /// 错误恢复
    /// </summary>
    private void Synchronize()
    {
        Advance();
        
        while (!IsAtEnd())
        {
            if (Previous().Type == TokenType.Semicolon) return;
            
            switch (Peek().Type)
            {
                case TokenType.Let:
                case TokenType.Var:
                case TokenType.If:
                case TokenType.For:
                case TokenType.Return:
                case TokenType.Import:
                    return;
            }
            
            Advance();
        }
    }
}
