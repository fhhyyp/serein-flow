using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Script.Node;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Serein.Script
{
    public ref struct SereinScriptParser
    {
        private SereinScriptLexer _lexer;
        private Token _currentToken;


        public SereinScriptParser(string script)
        {
            _lexer = new SereinScriptLexer(script); // 语法分析
            _currentToken = _lexer.NextToken();
        }

        public ASTNode Parse()
        {
            return Program();
        }

        

        private List<ASTNode> Statements {  get; } = new List<ASTNode>();

        private ASTNode Program()
        {
            Statements.Clear();
            while (_currentToken.Type != TokenType.EOF)
            {
                
                var astNode = Statement();
                if (astNode == null)
                {
                    continue;
                }
                Statements.Add(astNode);

                //if (astNode is ClassTypeDefinitionNode)
                //{
                //    statements = [astNode, ..statements]; // 类型定义置顶
                //}
                //else
                //{
                //    statements.Add(astNode);
                //}

            }
            return new ProgramNode(Statements).SetTokenInfo(_currentToken);
        }

        private ASTNode Statement()
        {
            if (_currentToken.Type == TokenType.Keyword && _currentToken.Value == "let")
            {
                return ParseLetAssignment();
            }
            if (_currentToken.Type == TokenType.Keyword && _currentToken.Value == "class")
            {
                return ParseClassDefinition(); // 加载类，如果已经加载过，则忽略
            }
            if (_currentToken.Type == TokenType.Keyword && _currentToken.Value == "new")
            {
                var _peekToken = _lexer.PeekToken();
                if (_peekToken.Type == TokenType.Keyword && _peekToken.Value == "class")
                {
                    return ParseClassDefinition(); // 重新加载类
                }
            }

            if (_currentToken.Type == TokenType.Keyword && _currentToken.Value == "if")
            {
                return ParseIf();
            }
            if (_currentToken.Type == TokenType.Keyword && _currentToken.Value == "while")
            {
                return ParseWhile();
            }
            if (_currentToken.Type == TokenType.Keyword && _currentToken.Value == "return")
            {
                return ParseReturn();
            }
            if (_currentToken.Type == TokenType.Identifier)
            {
                return ParseIdentifier();
            }
            if (_currentToken.Type == TokenType.Null)
            {
                return Expression();
            }



            // 处理其他语句（如表达式语句等）
            if (_currentToken.Type == TokenType.Semicolon)
            {
                _currentToken = _lexer.NextToken();
                return null; // 表示空语句
            }

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
            string variable = _currentToken.Value.ToString();
            _currentToken = _lexer.NextToken(); // Consume identifier
            ASTNode value;
            if (_currentToken.Type == TokenType.Semicolon)
            {
                // 定义一个变量，初始值为 null
                value = new NullNode();
            }
            else
            {
                if (_currentToken.Type != TokenType.Operator || _currentToken.Value != "=")
                    throw new Exception("Expected '=' after variable name");
                _currentToken = _lexer.NextToken();
                value = Expression();
                _currentToken = _lexer.NextToken(); // Consume semicolon
               
            }
            return new AssignmentNode(variable, value).SetTokenInfo(_currentToken);



        }

        private ASTNode ParseClassDefinition()
        {
            bool isOverlay = false;
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
                var fieldType = _currentToken.Value.ToString().ToTypeOfString(); // 获取定义的类名
                _currentToken = _lexer.NextToken(); 
                var fieldName = _currentToken.Value.ToString(); // 获取定义的类名
                _currentToken = _lexer.NextToken(); 
                classFields.Add(fieldName,fieldType);
                if (_currentToken.Type == TokenType.Semicolon && _lexer.PeekToken().Type == TokenType.BraceRight)
                {
                    break;
                }
                else
                {
                    _currentToken = _lexer.NextToken();
                }

            }

            _currentToken = _lexer.NextToken();
            _currentToken = _lexer.NextToken();
            return new ClassTypeDefinitionNode(classFields, className, isOverlay).SetTokenInfo(_currentToken);
        }

        public ASTNode ParseObjectInstantiation()
        {
            _currentToken = _lexer.NextToken(); // Consume "new"
            string typeName = _currentToken.Value.ToString(); // Get type name
            _currentToken = _lexer.NextToken();
            if (_currentToken.Type != TokenType.ParenthesisLeft)
                throw new Exception("Expected '(' after function name");

            _currentToken = _lexer.NextToken(); // consume "("

            var arguments = new List<ASTNode>();
            while (_currentToken.Type != TokenType.ParenthesisRight)
            {
                arguments.Add(Expression());
                if (_currentToken.Type == TokenType.Comma)
                {
                    _currentToken = _lexer.NextToken(); // consume ","
                }
            }

            _currentToken = _lexer.NextToken(); // consume ")"
            return new ObjectInstantiationNode(typeName, arguments).SetTokenInfo(_currentToken);
        }


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



        private ASTNode ParseMemberFunctionCall(ASTNode targetNode)
        {
            string functionName = _currentToken.Value.ToString();
            _currentToken = _lexer.NextToken(); // consume identifier

            if (_currentToken.Type != TokenType.ParenthesisLeft)
                throw new Exception("Expected '(' after function name");

            _currentToken = _lexer.NextToken(); // consume "("

            var arguments = new List<ASTNode>();
            while (_currentToken.Type != TokenType.ParenthesisRight)
            {
                var arg = Expression();
                _currentToken = _lexer.NextToken(); // consume arg
                arguments.Add(arg);
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


        private ASTNode ParseFunctionCall()
        {
            string functionName = _currentToken.Value.ToString();
            _currentToken = _lexer.NextToken(); // consume identifier

            if (_currentToken.Type != TokenType.ParenthesisLeft)
                throw new Exception("Expected '(' after function name");

            _currentToken = _lexer.NextToken(); // consume "("

            var arguments = new List<ASTNode>();
            while (_currentToken.Type != TokenType.ParenthesisRight)
            {
                var arg = Expression();
                _currentToken = _lexer.NextToken(); // consume arg
                arguments.Add(arg);
                if (_currentToken.Type == TokenType.Comma)
                {
                    _currentToken = _lexer.NextToken(); // consume ","
                }
                if (_currentToken.Type == TokenType.Semicolon)
                {
                    break; // consume ";"
                }
            }

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

        public ASTNode ParseReturn()
        {
            _currentToken = _lexer.NextToken();
            if(_currentToken.Type == TokenType.Semicolon)
            {
                return new ReturnNode().SetTokenInfo(_currentToken);
            }
            var resultValue = Expression();
            _currentToken = _lexer.NextToken();
            return new ReturnNode(resultValue).SetTokenInfo(_currentToken);
        }

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
                var astNode = Statement();
                if (astNode != null)
                {
                    trueBranch.Add(astNode);
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
                    var astNode = Statement();
                    if (astNode != null)
                    {
                        falseBranch.Add(astNode);
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
                body.Add(Statement());
            }
            _currentToken = _lexer.NextToken(); // Consume "}"
            return new WhileNode(condition, body).SetTokenInfo(_currentToken);
        }


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
                _currentToken = _lexer.NextToken();  // 消耗数字
                return new StringNode(text.ToString()).SetTokenInfo(_currentToken);
            }
            if( _currentToken.Type == TokenType.InterpolatedString)
            {
                // 可能是插值字符串；
                // let context = $"a{A}b{B}c";
                // let context = "a" + A + "b" + B + c;
                _currentToken = _lexer.NextToken(); // 消耗字符串
                while (_currentToken.Type == TokenType.String) { 
                }
            }

            if (_currentToken.Type == TokenType.Number)
            {
                var value = int.Parse(_currentToken.Value);
                _currentToken = _lexer.NextToken();  // 消耗数字
                return new NumberNode(value).SetTokenInfo(_currentToken);
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
                    return ParseFunctionCall();
                }

                // 需要从该标识符调用另一个标识符
                if (_identifierPeekToken.Type == TokenType.Dot)
                {
                    return ParseMemberAccessOrAssignment();
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
