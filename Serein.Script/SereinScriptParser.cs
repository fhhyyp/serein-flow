using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Library.Utils;
using Serein.Script.Node;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Serein.Script
{

    /// <summary>
    /// SereinScriptParser 用于解析 Serein 脚本语言的语法。
    /// </summary>
    public ref struct SereinScriptParser
    {
        private SereinScriptLexer _lexer;
        private Token _currentToken;


        public SereinScriptParser(string script)
        {
            _lexer = new SereinScriptLexer(script); // 语法分析
            _currentToken = _lexer.NextToken();
        }


        /// <summary>
        /// 解析脚本并返回 AST（抽象语法树）根节点。
        /// </summary>
        /// <returns></returns>
        public ASTNode Parse()
        {
            return Program();
        }

        

        private List<ASTNode> Statements {  get; } = new List<ASTNode>();

        /// <summary>
        /// 解析整个程序，直到遇到文件结尾（EOF）为止。
        /// </summary>
        /// <returns></returns>
        private ASTNode Program()
        {
            Statements.Clear();
            while (_currentToken.Type != TokenType.EOF)
            {
                
                var astNode = Statement(); // 解析单个语句
                if (astNode == null)
                {
                    continue;
                }
                Statements.Add(astNode); // 将解析得到的 AST 节点添加到语句列表中
            }
            var programNode = new ProgramNode(Statements);
            programNode.SetTokenInfo(_currentToken); // 程序节点，包含所有解析的语句列表

            SereinScriptTypeAnalysis typeAnalysis = new SereinScriptTypeAnalysis(programNode);
            return programNode;

            /*if (astNode is ClassTypeDefinitionNode)
            {
                statements = [astNode, ..statements]; // 类型定义置顶
            }
            else
            {
                statements.Add(astNode);
            }*/
        }

        /// <summary>
        /// 解析单个语句。
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private ASTNode Statement()
        {

            // 处理其他语句（如表达式语句等）
            while (_currentToken.Type == TokenType.Semicolon)
            {
                _currentToken = _lexer.NextToken();
            }
            if(_currentToken.Type == TokenType.EOF)
            {
                return null;
            }
            

            if (_currentToken.Type == TokenType.Keyword && _currentToken.Value == "let")
            {
                // 处理 let 变量赋值语句
                return ParseLetAssignment();
            }
            if (_currentToken.Type == TokenType.Keyword && _currentToken.Value == "class")
            {
                
                // 加载类定义
                return ParseClassDefinition(); // 加载类，如果已经加载过，则忽略
            }
            if (_currentToken.Type == TokenType.Keyword && _currentToken.Value == "new")
            {
                var _peekToken = _lexer.PeekToken();
                if (_peekToken.Type == TokenType.Keyword && _peekToken.Value == "class")
                {
                    // 重新加载类定义
                    return ParseClassDefinition(); // 重新加载类
                }
            }

            if (_currentToken.Type == TokenType.Keyword && _currentToken.Value == "if")
            {
                // 处理 if 语句
                return ParseIf();
            }
            if (_currentToken.Type == TokenType.Keyword && _currentToken.Value == "while")
            {
                // 处理 while 循环语句
                return ParseWhile();
            }
            if (_currentToken.Type == TokenType.Keyword && _currentToken.Value == "return")
            {
                // 处理 return 语句
                return ParseReturn();
            }
            if (_currentToken.Type == TokenType.Identifier)
            {
                // 处理标识符，可能是函数调用、变量赋值或对象成员访问等行为
                return ParseIdentifier();
            }
            if (_currentToken.Type == TokenType.Null)
            {
                // 处理 null 语句
                return Expression();
            }

            /*if (_currentToken.Type == TokenType.Semicolon)
            {
                _currentToken = _lexer.NextToken();
                return null; // 表示空语句
            }*/



            throw new Exception("Unexpected statement: " + _currentToken.Value.ToString());
        }


        /// <summary>
        /// 从标识符解析方法调用、变量赋值、获取对象成员行为。
        /// （非符号、关键字）
        /// </summary>
        /// <returns></returns>
        private ASTNode ParseIdentifier()
        {
            
            // 检查标识符后是否跟有左圆括号
            var _tempToken = _lexer.PeekToken();
            if (_tempToken.Type == TokenType.ParenthesisLeft)
            {
                // 解析函数调用
                return ParseFunctionCall();
            }
            else if (_tempToken.Type == TokenType.Dot)
            {
                // 对象成员的获取
                return ParseMemberAccessOrAssignment(); 
            }
            else if (_tempToken.Type == TokenType.SquareBracketsLeft)
            {
                // 数组 index; 字典 key obj.Member[xxx];
                return ParseCollectionIndex();
                
            }
            else
            {
                // 不是函数调用，是变量赋值或其他
                return ParseAssignment();
            }
        }

        
        /// <summary>
        /// 解析赋值行为
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private ASTNode ParseAssignment()
        {
            string variableName = _currentToken.Value.ToString();

            var _peekToken = _lexer.PeekToken();
            if (_peekToken.Type == TokenType.ParenthesisRight)
            {
                _currentToken = _lexer.NextToken();  // 消耗标识符

                return new IdentifierNode(variableName).SetTokenInfo(_currentToken);
            }

            if(_peekToken.Type == TokenType.Operator && _peekToken.Value == "=")
            {
                // 赋值行为
                _currentToken = _lexer.NextToken();  // 消耗标识符
                _currentToken = _lexer.NextToken();  // 消耗 "="
                var _tempToken = _lexer.PeekToken();
                ASTNode valueNode;

                if(_tempToken.Type == TokenType.Operator && _tempToken.Value != "=")
                {
                    //_currentToken = _lexer.NextToken(); // 消耗操作符
                    //_currentToken = _lexer.NextToken(); // 消耗操作符
                    valueNode = Expression();
                }
                else if (_tempToken.Type == TokenType.ParenthesisLeft)
                {
                    // 解析赋值右边的表达式
                    // 是函数调用，解析函数调用
                    valueNode = ParseFunctionCall();
                }
                else
                {
                    // 解析赋值右边的字面量表达式
                    valueNode = Expression();
                }
                return new AssignmentNode(variableName, valueNode).SetTokenInfo(_currentToken);
            }
            if (_peekToken.Type == TokenType.Dot)
            {
                // 可能是方法调用
                return ParseMemberAccessOrAssignment();
            }

            if(_peekToken.Type == TokenType.Operator)
            {
                return new IdentifierNode(variableName).SetTokenInfo(_currentToken);
            }
            
            
            throw new Exception($"Expected '{_currentToken.Value}' after variable name");

        }

        /// <summary>
        /// 解析 let 变量赋值行为
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private ASTNode ParseLetAssignment()
        {
            
            _currentToken = _lexer.NextToken(); // Consume "let"
            string variable = _currentToken.Value.ToString(); // 变量名称
            _currentToken = _lexer.NextToken(); // Consume identifier
            ASTNode value;
            AssignmentNode assignmentNode;
            if (_currentToken.Type == TokenType.Semicolon)
            {
                // 定义一个变量，初始值为 null
                value = new NullNode();
                assignmentNode = new AssignmentNode(variable, value); // 生成node
                assignmentNode.SetTokenInfo(_currentToken); // 设置token信息
            }
            else
            {
                // 如果定义了变量，后面紧跟着操作符，且操作符不是“=”话视为异常
                // let value = obj; 
                if (_currentToken.Type != TokenType.Operator || _currentToken.Value != "=")
                    throw new Exception("Expected '=' after variable name");
                _currentToken = _lexer.NextToken(); // 消耗操作符（“=”）
                var nodeToken = _currentToken;
                value = Expression(); // 解析获取赋值表达式
                assignmentNode = new AssignmentNode(variable, value); // 生成node
                assignmentNode.SetTokenInfo(nodeToken); // 设置token信息
                _currentToken = _lexer.NextToken(); // 消耗分号
            }
            return assignmentNode;



        }

        /// <summary>
        /// 解析类定义
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private ASTNode ParseClassDefinition()
        {
            var sb = new StringBuilder(); // 收集代码信息
            /*
             * 有两种定义类型的方式：
             *   1. class MyClass{}
             *   2. new class MyClass{} 
             * 解析执行时，第二种方式定义的类，会顶掉其他地方创建的“MyClass”同名类型；
             */
            var coreStartRangeIndex =  _lexer.GetIndex() - "class".Length; // 从“class”开始锚定代码范围
            bool isOverlay = false; // 指示是否覆盖缓存中创建过的同名其它类型
            
            if (_currentToken.Value == "new")
            {
                isOverlay = true; // 重新加载类
                _currentToken = _lexer.NextToken(); // 消耗 new 关键字
            }
            _currentToken = _lexer.NextToken(); // 消耗 class 关键字
            var className = _currentToken.Value.ToString(); // 获取定义的类名
            _currentToken = _lexer.NextToken(); // 消耗类名
            if (_currentToken.Type != TokenType.BraceLeft || _currentToken.Value != "{")
                throw new Exception("Expected '{' after class definition");
            var classFields = new Dictionary<string, Type>();
            _currentToken = _lexer.NextToken();  // 消耗括号

            while (_currentToken.Type != TokenType.BraceRight)
            {
                // 获取类字段定义
                var fieldType = _currentToken.Value.ToString().ToTypeOfString(); // 获取字段的类型
                _currentToken = _lexer.NextToken();  // 消耗类型
                var fieldName = _currentToken.Value.ToString(); // 获取定义的类名
                _currentToken = _lexer.NextToken();  // 消耗字段名称
                classFields.Add(fieldName,fieldType); // 添加字段
                if (_currentToken.Type == TokenType.Semicolon 
                    && _lexer.PeekToken().Type == TokenType.BraceRight)
                {
                    // 如果遇到分号、大括号，退出字段定义。
                    break;
                }
                else
                {
                    _currentToken = _lexer.NextToken();
                }

            }
            _currentToken = _lexer.NextToken(); // 消耗类型定义 } 括号
            var typeDefinitionCode = _lexer.GetCoreContent(coreStartRangeIndex); // 收集类型定义的代码。（在Statement方法中开始收集的）
            var node = new ClassTypeDefinitionNode(classFields, className, isOverlay);
            _currentToken.Code = typeDefinitionCode;
            node.SetTokenInfo(_currentToken);
            // _currentToken = _lexer.NextToken();
            _currentToken = _lexer.NextToken(); 
            return node;
        }

        /// <summary>
        /// 解析对象实例化行为
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public ASTNode ParseObjectInstantiation()
        {
            _currentToken = _lexer.NextToken(); // 消耗 new 关键字
            string typeName = _currentToken.Value.ToString(); // 获取类型名称
            _currentToken = _lexer.NextToken();
            if (_currentToken.Type != TokenType.ParenthesisLeft)
                throw new Exception("Expected '(' after function name");

            _currentToken = _lexer.NextToken(); // 消耗 "("

            var arguments = new List<ASTNode>();
            while (_currentToken.Type != TokenType.ParenthesisRight)
            {
                arguments.Add(Expression()); // 获取参数表达式
                if (_currentToken.Type == TokenType.Comma)
                {
                    _currentToken = _lexer.NextToken(); // consume ","
                }
            }

            _currentToken = _lexer.NextToken(); // 消耗 ")"
            return new ObjectInstantiationNode(typeName, arguments).SetTokenInfo(_currentToken);
        }

        /// <summary>
        /// 解析集合索引行为（数组或字典）
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public ASTNode ParseCollectionIndex()
        {
            var identifierNode = new IdentifierNode(_currentToken.Value.ToString()).SetTokenInfo(_currentToken);

            string collectionName = _currentToken.Value.ToString();
            //_lexer.NextToken(); // consume "["
            _currentToken = _lexer.NextToken(); // consume identifier
            // ParenthesisLeft
            if (_currentToken.Type != TokenType.SquareBracketsLeft)
                throw new Exception("Expected '[' after function name");

            _currentToken = _lexer.NextToken(); // consume "["

            ASTNode indexValue = Expression(); // get index value

            _currentToken = _lexer.NextToken(); // consume "]"
            return new CollectionIndexNode(identifierNode,indexValue).SetTokenInfo(_currentToken);
        }

        /// <summary>
        /// 获取对象成员
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private ASTNode ParseMemberAccessOrAssignment()
        {
            var identifierNode = new IdentifierNode(_currentToken.Value).SetTokenInfo(_currentToken);
             _currentToken = _lexer.NextToken(); // 消耗当前标识符
            

            // 处理成员访问：identifier.member
            if (_currentToken.Type == TokenType.Dot)
            {
                _currentToken = _lexer.NextToken(); // 消耗 "."
                if (_currentToken.Type != TokenType.Identifier)
                {
                    throw new Exception("Expected member name after dot.");
                }

                var memberName = _currentToken.Value;
                //_currentToken = _lexer.NextToken(); // 消耗成员名

                var _peekToken = _lexer.PeekToken();
                if (_peekToken.Type == TokenType.Operator && _peekToken.Value == "=")
                {
                    // 成员赋值 obj.Member = xxx;
                    _currentToken = _lexer.NextToken(); // 消耗 "="
                    _currentToken = _lexer.NextToken(); // 消耗 "="
                    var valueNode = Expression();  // 解析右值
                    return new MemberAssignmentNode(identifierNode, memberName, valueNode).SetTokenInfo(_peekToken);
                }
                else
                {

                    if(_peekToken.Type == TokenType.ParenthesisLeft)
                    {
                        // 成员方法调用 obj.Member(xxx);
                        return ParseMemberFunctionCall(identifierNode);
                    }
                    else if (_peekToken.Type == TokenType.SquareBracketsLeft)
                    {
                        // 数组 index; 字典 key obj.Member[xxx];
                        return ParseCollectionIndex();
                    }
                    else
                    {

                        _currentToken = _lexer.NextToken(); // 消耗 成员名称
                        // 成员获取
                        return new MemberAccessNode(identifierNode, memberName).SetTokenInfo(_currentToken);
                    }

                }
            }

            return identifierNode;
        }


        /// <summary>
        /// 解析成员函数调用行为
        /// </summary>
        /// <param name="targetNode"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private ASTNode ParseMemberFunctionCall(ASTNode targetNode)
        {
            string functionName = _currentToken.Value.ToString(); // 函数名称
            _currentToken = _lexer.NextToken(); // consume identifier

            if (_currentToken.Type != TokenType.ParenthesisLeft)
                throw new Exception("Expected '(' after function name");

            _currentToken = _lexer.NextToken(); // consume "("

            var arguments = new List<ASTNode>();
            while (_currentToken.Type != TokenType.ParenthesisRight)
            {
                // 获取参数表达式
                var arg = Expression();
                _currentToken = _lexer.NextToken(); // consume arg
                arguments.Add(arg);  // 添加到参数列表
                if (_currentToken.Type == TokenType.Comma)
                {
                    _currentToken = _lexer.NextToken(); // consume ","
                }
                if (_currentToken.Type == TokenType.Semicolon)
                {
                    break; // consume ";"
                }
            }

            //_currentToken = _lexer.NextToken(); // consume ")"

            return new MemberFunctionCallNode(targetNode, functionName, arguments).SetTokenInfo(_currentToken);
        }


        /// <summary>
        /// 解析函数调用行为
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private ASTNode ParseFunctionCall()
        {
            string functionName = _currentToken.Value.ToString();
            _currentToken = _lexer.NextToken();  // consume identifier

            if (_currentToken.Type != TokenType.ParenthesisLeft)
                throw new Exception("Expected '(' after function name");

            _currentToken = _lexer.NextToken(); // consume "("

            var arguments = new List<ASTNode>();
            bool isBreak = false;
            while (_currentToken.Type != TokenType.ParenthesisRight)
            {
                var arg = Expression(); // 获取参数表达式
                _currentToken = _lexer.NextToken(); // consume arg
                arguments.Add(arg);
                if (_currentToken.Type == TokenType.Comma)
                {
                    _currentToken = _lexer.NextToken(); // consume ","
                }
                if (_currentToken.Type == TokenType.Semicolon)
                {
                    isBreak = true;
                    break; // consume ";"
                }
            }
            if(!isBreak)
                _currentToken = _lexer.NextToken(); // consume ")"

           
            //var node = Statements[^1];
            //if (node is MemberAccessNode memberAccessNode)
            //{
            //    // 上一个是对象
            //    return new MemberFunctionCallNode(memberAccessNode, functionName, arguments).SetTokenInfo(_currentToken);
            //}
            //if (node is IdentifierNode identifierNode)
            //{
            //    return new MemberFunctionCallNode(identifierNode, functionName, arguments).SetTokenInfo(_currentToken);
            //}

            // 从挂载的函数表寻找对应的函数，尝试调用
            return new FunctionCallNode(functionName, arguments).SetTokenInfo(_currentToken);


        }

        /// <summary>
        /// 解析 return 语句。
        /// </summary>
        /// <returns></returns>
        public ASTNode ParseReturn()
        {
            _currentToken = _lexer.NextToken();
            if(_currentToken.Type == TokenType.Semicolon)
            {
                return new ReturnNode().SetTokenInfo(_currentToken); // 返回空的 ReturnNode
            }
            var resultValue = Expression(); // 获取返回值表达式
            _currentToken = _lexer.NextToken(); 
            return new ReturnNode(resultValue).SetTokenInfo(_currentToken); 
        }


        /// <summary>
        /// 解析 if 语句。
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private ASTNode ParseIf()
        {
            _currentToken = _lexer.NextToken(); // Consume "if"
            _currentToken = _lexer.NextToken(); // Consume "("
            ASTNode condition = Expression();
            _currentToken = _lexer.NextToken(); // Consume ")"

            // 确保遇到左大括号 { 后进入代码块解析
            if (_currentToken.Type != TokenType.BraceLeft)
            {
                throw new Exception("Expected '{' after if condition");
            }
            _currentToken = _lexer.NextToken(); // Consume "{"

            // 解析大括号中的语句
            List<ASTNode> trueBranch = new List<ASTNode>();
            List<ASTNode> falseBranch = new List<ASTNode>();
            while (_currentToken.Type != TokenType.BraceRight && _currentToken.Type != TokenType.EOF)
            {
                var astNode = Statement(); // 解析 if 分支中的语句
                if (astNode != null)
                {
                    trueBranch.Add(astNode); // 将 if 分支的语句添加到 trueBranch 中
                }
            }
            // 确保匹配右大括号 }
            if (_currentToken.Type != TokenType.BraceRight)
            {
                throw new Exception("Expected '}' after if block");
            }
            _currentToken = _lexer.NextToken(); // Consume "}"
            if (_currentToken.Type == TokenType.Keyword && _currentToken.Value == "else")
            {
                _currentToken = _lexer.NextToken(); // Consume "{"
                _currentToken = _lexer.NextToken(); // Consume "{"
                while (_currentToken.Type != TokenType.BraceRight && _currentToken.Type != TokenType.EOF)
                {
                    var astNode = Statement(); // 解析 else 分支中的语句
                    if (astNode != null)
                    {
                        falseBranch.Add(astNode); // 将 else 分支的语句添加到 falseBranch 中
                    }
                }
                // 确保匹配右大括号 }
                if (_currentToken.Type != TokenType.BraceRight)
                {
                    throw new Exception("Expected '}' after if block");
                }
                _currentToken = _lexer.NextToken(); // Consume "}"
            }


            return new IfNode(condition, trueBranch, falseBranch).SetTokenInfo(_currentToken);
        }

        /// <summary>
        /// 解析 while 循环语句。
        /// </summary>
        /// <returns></returns>
        private ASTNode ParseWhile()
        {
            _currentToken = _lexer.NextToken(); // Consume "while"
            _currentToken = _lexer.NextToken(); // Consume "("
            ASTNode condition = Expression();
            _currentToken = _lexer.NextToken(); // Consume ")"
            _currentToken = _lexer.NextToken(); // Consume "{"
            List<ASTNode> body = new List<ASTNode>();
            while (_currentToken.Type != TokenType.BraceRight)
            {
                body.Add(Statement()); // 解析循环体中的语句
            }
            _currentToken = _lexer.NextToken(); // Consume "}"
            return new WhileNode(condition, body).SetTokenInfo(_currentToken);
        }

        /// <summary>
        /// 解析表达式。
        /// </summary>
        /// <returns></returns>
        private ASTNode Expression()
        {
            ASTNode left = Term();
            while (_currentToken.Type == TokenType.Operator && (
                _currentToken.Value == "+" || _currentToken.Value == "-" ||
                _currentToken.Value == "*" || _currentToken.Value == "/"))
            {
                string op = _currentToken.Value.ToString();
                _currentToken = _lexer.NextToken();
                ASTNode right = Term();
                left = new BinaryOperationNode(left, op, right).SetTokenInfo(_currentToken);
            }
          return left;
        }


        /// <summary>
        /// 解析项（Term），用于处理加减乘除等运算符。
        /// </summary>
        /// <returns></returns>
        private ASTNode Term()
        {
            ASTNode left = Factor();
            while (_currentToken.Type == TokenType.Operator &&
                (_currentToken.Value == "<" || _currentToken.Value == ">" ||
                _currentToken.Value == "<=" || _currentToken.Value == ">=" ||
                _currentToken.Value == "==" || _currentToken.Value == "!="))
            {
                string op = _currentToken.Value.ToString();
                _currentToken = _lexer.NextToken();
                ASTNode right = Factor();
                left = new BinaryOperationNode(left, op, right).SetTokenInfo(_currentToken);
            }
            return left;
        }

        /// <summary>
        /// 解析因子（Factor），用于处理基本的字面量、标识符、括号表达式等。
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private ASTNode Factor()
        {
            #region 返回字面量
            if (_currentToken.Type == TokenType.Null)
            {
                _currentToken = _lexer.NextToken(); // 消耗 null
                return new NullNode().SetTokenInfo(_currentToken);
            }
            if (_currentToken.Type == TokenType.Boolean)
            {
                var value = bool.Parse(_currentToken.Value);
                _currentToken = _lexer.NextToken();  // 消耗布尔量
                return new BooleanNode(value).SetTokenInfo(_currentToken);
            }
            if (_currentToken.Type == TokenType.String)
            {
                var text = _currentToken.Value;
                var node = new StringNode(text).SetTokenInfo(_currentToken); 
                _currentToken = _lexer.NextToken();  // 消耗字符串
                return node;
            }
            if (_currentToken.Type == TokenType.Char)
            {
                var text = _currentToken.Value;
                _currentToken = _lexer.NextToken();  // 消耗Char
                return new CharNode(text).SetTokenInfo(_currentToken);
            }
            if( _currentToken.Type == TokenType.InterpolatedString)
            {
                // 暂未实现插值字符串
                // 可能是插值字符串；
                // let context = $"a{A}b{B}c";
                // let context = "a" + A + "b" + B + c;
                _currentToken = _lexer.NextToken(); // 消耗字符串
                while (_currentToken.Type == TokenType.String) { 
                }
            }

            if (_currentToken.Type == TokenType.NumberInt)
            {
                var value = int.Parse(_currentToken.Value);
                _currentToken = _lexer.NextToken();  // 消耗 int 整型
                return new NumberIntNode(value).SetTokenInfo(_currentToken);
            }

            if (_currentToken.Type == TokenType.NumberLong)
            {
                var value = long.Parse(_currentToken.Value);
                _currentToken = _lexer.NextToken();  // 消耗 
                return new NumberLongNode(value).SetTokenInfo(_currentToken);
            }

            if (_currentToken.Type == TokenType.NumberFloat)
            {
                var value = float.Parse(_currentToken.Value);
                _currentToken = _lexer.NextToken();  // 消耗数字
                return new NumberFloatNode(value).SetTokenInfo(_currentToken);
            }

            if (_currentToken.Type == TokenType.NumberDouble)
            {
                var value = float.Parse(_currentToken.Value);
                _currentToken = _lexer.NextToken();  // 消耗数字
                return new NumberDoubleNode(value).SetTokenInfo(_currentToken);
            }
            #endregion

            // 方法调用
            if (_currentToken.Type == TokenType.ParenthesisLeft)
            {
                _currentToken = _lexer.NextToken();  // 消耗 "("
                var expr = Expression();
                if (_currentToken.Type != TokenType.ParenthesisRight)
                    throw new Exception("非预期的符号，预期符号为\")\"。");
                _currentToken = _lexer.NextToken();  // 消耗 ")"
                return expr;
            }

            // 创建对象
            if (_currentToken.Type == TokenType.Keyword && _currentToken.Value == "new")
            {
                // 可能是对象实例化
                return ParseObjectInstantiation();
            }

            // 标识符节点
            if (_currentToken.Type == TokenType.Identifier)
            {
                var identifier = _currentToken.Value; // 标识符字面量
                var _identifierPeekToken = _lexer.PeekToken();
                // 该标识符是方法调用
                if (_identifierPeekToken.Type == TokenType.ParenthesisLeft)
                {
                    // 可能是函数调用
                    return ParseFunctionCall();
                }

                // 需要从该标识符调用另一个标识符
                if (_identifierPeekToken.Type == TokenType.Dot)
                {
                    // 可能是成员访问或成员赋值
                    return ParseMemberAccessOrAssignment(); // 二元操作中获取对象成员
                }


                // 数组 index; 字典 key obj.Member[xxx];
                if (_identifierPeekToken.Type == TokenType.SquareBracketsLeft)
                {
                    return ParseCollectionIndex();
                }

                _currentToken = _lexer.NextToken();  // 消耗标识符
                return new IdentifierNode(identifier.ToString()).SetTokenInfo(_currentToken);
            }

           
            throw new Exception("Unexpected factor: " + _currentToken.Value.ToString());
        }
    }

}
