using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Library.Utils;
using Serein.Script.Node;
using Serein.Script.Node.FlowControl;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace Serein.Script
{

    /// <summary>
    /// SereinScriptParser 用于解析 Serein 脚本语言的语法。
    /// </summary>
    public ref struct SereinScriptParser
    {
        private SereinScriptLexer _lexer;
        private Token _currentToken;


        public SereinScriptParser()
        {
            
        }


        /// <summary>
        /// 解析脚本并返回 AST（抽象语法树）根节点。
        /// </summary>
        /// <returns></returns>
        public ProgramNode Parse(string script)
        {
            _lexer = new SereinScriptLexer(script); // 语法分析
            _currentToken = _lexer.NextToken();
            return Program();
        }

        private List<ASTNode> Statements {  get; } = new List<ASTNode>();

        /// <summary>
        /// 解析整个程序，直到遇到文件结尾（EOF）为止。
        /// </summary>
        /// <returns></returns>
        private ProgramNode Program()
        {
            Statements.Clear();
            while (_currentToken.Type != TokenType.EOF)
            {
                
                //var astNode = Statement(); // 解析单个语句
                var astNode = Statement2(); // 解析单个语句
                if (astNode == null)
                {
                    continue;
                }
                Statements.Add(astNode); // 将解析得到的 AST 节点添加到语句列表中
            }
            var programNode = new ProgramNode(Statements);
            programNode.SetTokenInfo(_currentToken); // 程序节点，包含所有解析的语句列表

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

        private  ASTNode Statement2()
        {

            //_currentToken = _lexer.NextToken(); // 获取新的Token
            if (_currentToken.Type == TokenType.Identifier)
            {
                /*  赋值语句与一般语句的表达形式
                 1. 赋值语句：
                        目标对象       表达式获取
                        Target() | = | Expression()            |
                                 | = | variable;               | (变量）
                                 | = | value;                  | 显式设置的字面量。
                                 | = | obj.Value...;           | obj为之前的上下文中出现过的变量，调用表达式包含对象成员数组、方法
                                 | = | array[...];             | array为之前的上下文中出现过的变量。
                                 | = | array[...].Value...;    | array为之前的上下文中出现过的变量。调用表达式包含对象成员数组、方法
                                 | = | new Class(...);         | 实例化类型，包含构造函数
                                 
                        补充：Target() 可能的成员
                           1. variable
                           2. obj.Value...
                           3. array[index]
                           4. array[index].Value...

                 2. 一般语句（方法调用）：
                        1. localFunc(...);
                        2. obj....Func(...);
                        3. array[]....Func(...);
                 */

                #region 分析是赋值语句还是一般语句。赋值语句中间存在 = 操作符，一般语句不存在。
                var backupToken = _currentToken; // 保存当前token
                var peekCount = 0; // peek次数
                var isAssignment = false;  // 指示操作类型，true代表赋值语句，false代表一般语句
                while (true)
                {
                    // 分析阶段，不移动token
                    var peekToken = _lexer.PeekToken(peekCount++); // 预览下一个节点
                    if (peekToken.Type == TokenType.Semicolon) break; // 遇到分号，结束词法分析
                    if (JudgmentOperator(peekToken, "="))
                    {
                        isAssignment = true;
                        break;
                    }
                    //if (peekToken.StartIndex >= _lexer.CodeLength) throw new Exception();
                }
                #endregion
                #region 生成 ASTNode
                if (isAssignment)
                {
                    // 以赋值语句的形式进行处理
                    var assignmentNode = ParseAssignmentNode();
                    NextToken();// 消耗 ";"
                    return assignmentNode;
                }
                else
                {
                    // 以一般语句的形式进行处理，可当作表达式进行解析
                    var targetNode = ParserExpression(); // 解析表达式
                    NextToken();// 消耗 ";"
                    return targetNode;
                } 
                #endregion

            }
            else if (JudgmentKeyword("class"))  // 定义对象
            {
                var classDefinitionNode = ParseClassDefinitionNode();
                return classDefinitionNode;
            }
            else if (JudgmentKeyword("return"))  // 返回语句
            {
                var returnNode = ParseReturnNode();
                return returnNode;
            }
            else if (_currentToken.Type == TokenType.Keyword)  // 语句块
            {
                /*
                 可能的关键字
                    1. if()...else...
                    2. while(){...}
                 */
                return null;
            }

            if(_currentToken.Type == TokenType.Semicolon)
            {
                NextToken(); // 消耗 ";"
                return null;
            }
            throw new Exception("解析异常");
            /*else
            {
                // 为避免解析异常，除了 Statement() 方法以外，其它地方不需要对  TokenType.Semicolon 进行处理
                throw new Exception("解析异常， Statement() 方法以外的地方处理了 TokenType.Semicolon ");
            }*/
        }

        /// <summary>
        /// 解析赋值语句
        /// </summary>
        /// <returns></returns>
        public ASTNode ParseAssignmentNode()
        {
            /*
             赋值语句：
                 目标对象       表达式获取
                 Target() | = | Expression()            |
                          | = | variable;               | (变量）
                          | = | value;                  | 显式设置的字面量。
                          | = | obj.Value...;           | obj为之前的上下文中出现过的变量，调用表达式包含对象成员数组、方法
                          | = | array[...];             | array为之前的上下文中出现过的变量。
                          | = | array[...].Value...;    | array为之前的上下文中出现过的变量。调用表达式包含对象成员数组、方法
                          | = | new Class(...);         | 实例化类型，包含构造函数

                 补充：Target() 可能的成员
                    1. variable
                    2. obj.Value...
                    3. array[index]
                    4. array[index].Value...

             循环获取token判断
                如果是 "." ，代表需要从对象中获取成员（属性、方法调用、数组）
                如果是 "(" ，代表调用挂载的本地方法
                如果是 "[" ，代表需要获取数组索引
                如果是 "=" ，代表是变量赋值，退出循环
             */
            /*
                  1. 获取成员      => PeekToken.Type = TokenType.Dot
                  2. 获取集合      => PeekToken.Type = TokenType.SquareBracketsLeft
                  3. 调用成员方法  => PeekToken.Type = TokenType.ParenthesisLeft
                 */
            //if (JudgmentOperator(_currentToken, "=")) break; // 退出
            //var tempPeekToken = _lexer.PeekToken();
            var backupToken = _currentToken;
            var targetNode = new IdentifierNode(_currentToken.Value).SetTokenInfo(_currentToken); // 生成 Tagget 标记节点
            NextToken(); // 消耗当前标识符的定义
            List<ASTNode> nodes = [targetNode];
            ASTNode? source = null;
            if (!JudgmentOperator(_currentToken, "="))
            {
                var peekToken2 = _lexer.PeekToken(); // 消耗 第一个标识符
                if (peekToken2.Type == TokenType.ParenthesisLeft)
                {
                    // 解析调用挂载方法
                    // ... = variable()()();
                    // 暂时不支持柯里化调用... = variable()()();
                    var functionCallNode = ParseFunctionCallNode();
                    if (_currentToken.Type == TokenType.Semicolon)
                    {
                        return functionCallNode;
                    }
                    targetNode = functionCallNode;
                }
                else if (peekToken2.Type == TokenType.SquareBracketsLeft)
                {
                    // 解析集合获取
                    var collectionIndexNode = ParseCollectionIndexNode(targetNode);
                    if (_currentToken.Type == TokenType.Semicolon)
                    {
                        return collectionIndexNode;
                    }
                    targetNode = collectionIndexNode;
                }

                // 开始解析
                while (true)
                {
                    if (JudgmentOperator(_currentToken, "=")) break; // 退出
                    var peekToken = _currentToken; // _lexer.PeekToken(); // 获取下一个token开始判断
                    source = nodes[^1]; // 重定向节点
                    if (peekToken.Type == TokenType.Dot) // 从对象获取
                    {
                        /*
                          1. 获取成员      => PeekToken.Type = TokenType.Dot
                          2. 获取集合      => PeekToken.Type = TokenType.SquareBracketsLeft
                          3. 调用成员方法  => PeekToken.Type = TokenType.ParenthesisLeft
                         */
                        NextToken(); // 消耗 "." 并获取下一个成员。
                        var peekToken3 = _lexer.PeekToken();
                        if (JudgmentOperator(peekToken3, "="))
                        {
                            var tempMemberAccessNode = ParseMemberAccessNode(source); // 获取对象中的成员 source.Value...
                            nodes.Add(tempMemberAccessNode);
                            break;
                        }
                        ASTNode tempNode = peekToken3.Type switch
                        {
                            TokenType.Dot => ParseMemberAccessNode(source), // 获取对象中的成员 source.Value...
                            TokenType.Semicolon => ParseMemberAccessNode(source), // 获取对象中的成员 source.Value...
                            TokenType.ParenthesisRight => ParseMemberAccessNode(source), // 获取对象中的成员 source.Value...
                            TokenType.SquareBracketsLeft => ParseCollectionIndexNode(source), // 获取集合中的元素 source[index]....
                            TokenType.ParenthesisLeft => ParseMemberFunctionCallNode(source), // 获取需要调用的方法 source(arg1,arg2...)...
                            _ => throw new Exception($"无法从对象获取成员，当前Token类型为 {peekToken.Type}。")
                        };
                        nodes.Add(tempNode);
                        continue; // 结束当前轮次的token判断

                    }
                    else if (peekToken.Type == TokenType.ParenthesisLeft) // 调用对象方法
                    {
                        var memberFunctionCallNode = ParseMemberFunctionCallNode(source);
                        nodes.Add(memberFunctionCallNode);
                        continue; // 结束当前轮次的token判断

                    }
                    else if (peekToken.Type == TokenType.SquareBracketsLeft) // 集合获取
                    {
                        var collectionIndexNode = ParseCollectionIndexNode(source);
                        nodes.Add(collectionIndexNode);
                        continue; // 结束当前轮次的token判断
                    }
                    /*else if (peekToken.Type == TokenType.Semicolon)
                    {
                        break;
                    }
                    else
                    {
                        if (peekToken.Type == TokenType.ParenthesisRight // 可能解析完了方法参数
                            || peekToken.Type == TokenType.Comma // 可能解析完了方法参数
                            || peekToken.Type == TokenType.ParenthesisRight) // 可能解析完了下标索引
                        {
                            return source;
                        }
                        // 应该是异常，如果是其它符号，说明词法解析不符合预期
                        throw new Exception($"在 Expression().Factor() 遇到意外的 TokenType ,{_currentToken.Type} {_currentToken.Value} ");
                    }*/
                }
                
            }
            targetNode = nodes[^1];
            if (targetNode is FunctionCallNode or MemberFunctionCallNode)
            {
                throw new Exception($"赋值语句左值部分不允许为方法调用,{targetNode.Code}");
            }
            else
            {
                // 反转赋值。
                NextToken(); // 消耗 "=" 并获取赋值语句的右值表达式。
                ASTNode valueNode = ParserExpression();
                if (targetNode is MemberAccessNode memberAccessNode)
                {
                    MemberAssignmentNode memberAssignmentNode = new MemberAssignmentNode(memberAccessNode.Object, memberAccessNode.MemberName, valueNode);
                    return memberAssignmentNode;
                }
                else if (targetNode is CollectionIndexNode collectionIndeNode)
                {
                    CollectionAssignmentNode collectionAssignmentNode = new CollectionAssignmentNode(collectionIndeNode, valueNode);
                    return collectionAssignmentNode;
                }
                else
                {
                    // 获取赋值节点
                    AssignmentNode assignmentNode = new AssignmentNode(target: targetNode, value: valueNode);
                    return assignmentNode;
                }
                
            }
           
        }

        /// <summary>
        /// （不处理分号）解析获取“集合获取”AST节点
        /// </summary>
        /// <param name="sourceNode"></param>
        /// <returns></returns>
        public MemberAccessNode ParseMemberAccessNode(ASTNode sourceNode)
        {
            string memberName = _currentToken.Value; // 成员名称
            var memberAccessNode = new MemberAccessNode(sourceNode, memberName);
            NextToken(); // 消耗成员名称
            memberAccessNode.SetTokenInfo(_currentToken);
            return memberAccessNode;
        }

        /// <summary>
        /// （不处理分号）解析获取“集合获取”AST节点
        /// </summary>
        /// <param name="sourceNode"></param>
        /// <returns></returns>
        public CollectionIndexNode ParseCollectionIndexNode(ASTNode sourceNode)
        {
            var collectionToken = _currentToken;
            string collectionName = _currentToken.Value; // 集合名称
            NextToken(TokenType.SquareBracketsLeft); // 消耗集合名称
            NextToken(); // 消耗 "[" 集合标识符的左中括号
            ASTNode indexNode = ParserExpression(); // 解析获取索引Node
            NextToken(); // 消耗 "]" 集合标识符的右中括号
            
            if(sourceNode is IdentifierNode)
            {
                var collectionIndexNode = new CollectionIndexNode(sourceNode, indexNode);
                collectionIndexNode.SetTokenInfo(collectionToken); // 表示获取集合第几个索引
                return collectionIndexNode;
            }
            else
            {
               
                var memberAccessNode = new MemberAccessNode(sourceNode, collectionName).SetTokenInfo(_currentToken);  // 表示集合从上一轮获取到的成员获取
                var collectionIndexNode = new CollectionIndexNode(memberAccessNode, indexNode);
                collectionIndexNode.SetTokenInfo(collectionToken); // 表示获取集合第几个索引
                return collectionIndexNode;
            }
            
        }

        /// <summary>
        /// （不处理分号）解析获取“对象方法调用”AST节点
        /// </summary>
        /// <param name="sourceNode"></param>
        /// <returns></returns>
        public MemberFunctionCallNode ParseMemberFunctionCallNode(ASTNode sourceNode)
        {
            string methodName = _currentToken.Value; // 方法名称
            NextToken(TokenType.ParenthesisLeft); // 消耗方法名称
            List<ASTNode> argNodes = []; // 方法参数Node
            NextToken(); // 消耗 "(" 
            while (_currentToken.Type != TokenType.ParenthesisRight) // 遇到 ")" 表示参数获取完毕
            {
                ASTNode node = ParserArgNode(); // 解析方法调用参数
                argNodes.Add(node);
                if (_currentToken.Type == TokenType.Comma)
                {
                    NextToken(); // 消耗参数分隔符 "," 
                }
            }
            NextToken(); // 消耗 ")" 表示参数获取完毕
            var memberFunctionCallNode = new MemberFunctionCallNode(sourceNode, methodName, argNodes);
            memberFunctionCallNode.SetTokenInfo(_currentToken);
            return memberFunctionCallNode; // 结束当前轮次的token判断
        }

        /// <summary>
        /// （不处理分号）解析获取“调用挂载的方法”AST节点
        /// </summary>
        /// <returns></returns>
        public FunctionCallNode ParseFunctionCallNode()
        {
            string functionName = _currentToken.Value; // 方法名称
            NextToken(TokenType.ParenthesisLeft); // 消耗方法名称
            List<ASTNode> argNodes = []; // 方法参数Node、
            NextToken(); // 消耗 "(" 
            while (_currentToken.Type != TokenType.ParenthesisRight) // 遇到 ")" 表示参数获取完毕
            {
                ASTNode node = ParserArgNode(); // 解析挂载方法的入参
                argNodes.Add(node);
                if (_currentToken.Type == TokenType.Comma)
                {
                    NextToken(); // 消耗参数分隔符 "," 
                }
            }
            NextToken(); // 消耗 ")" 
            var functionCallNode = new FunctionCallNode(functionName, argNodes); 
            functionCallNode.SetTokenInfo(_currentToken);
            return functionCallNode;
        }

        /// <summary>
        /// （不处理分号，逗号，右括号）用于解析(...)中的参数部分。终止条件是 "," 参数分隔符 和 ")" 方法入参终止符
        /// </summary>
        /// <returns></returns>
        public ASTNode ParserArgNode()
        {
            ASTNode node = ParserExpression(); // 解析参数
            // ParserExpression 会完全解析当前表达式，自动移动到下一个Token，所以需要在这里判断是否符合期望的TokenType
            if(_currentToken.Type == TokenType.Comma || _currentToken.Type == TokenType.ParenthesisRight)
            {
                return node;
            }
            throw new Exception($"解析参数节点后，当前Token类型不符合预期，当前类型为 {_currentToken.Type}。");
        }

        /// <summary>
        /// （没有分号）解析类定义 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private ASTNode ParseClassDefinitionNode()
        {
            /*
             class [ClassName] { 
                FieldType FieldName1;
                FieldType FieldName2;
                FieldType FieldName3;
                FieldType FieldName4;
             }
             */
            var coreStartRangeIndex = _lexer.GetIndex(); // 从“class”开始锚定代码范围
            if (!JudgmentKeyword("class")) throw new Exception($"解析类型定义节点时，当前Token不为关键字“class”。");
            NextToken(TokenType.Identifier); // 消耗“class”关键字，获取类名
            string className = _currentToken.Value; // 类型名称
            NextToken(TokenType.BraceLeft); // 消耗类型名称，并判断是否为 "{"
            NextToken();
            var classFields = new Dictionary<string, Type>(); // 记录类型中定义的字段
            while (_currentToken.Type != TokenType.BraceRight) // 遇到 "}" 时表示类型解析完毕
            {
                // 获取字段的类型、名称，类型允许包含逗号（使用C#中的类型）
                /*
                 class User { 
                    System.Int32 Age; // 等效于 int Age;
                 }
                字段解析时的预期顺序：
                      1     2      3     4      5        6
                    [id -> dot -> id -> dot -> id] ->    id    -> Semicolon
                    [         字段定义           ]    字段名称     解析完成
                 */
                string fieldTypeName = _currentToken.Value;
                string fieldName;
                while (true) 
                {
                    var peekToken = _lexer.PeekToken();
                    if (peekToken.Type == TokenType.Dot)
                    {
                        NextToken(TokenType.Identifier);
                        fieldTypeName = $"{fieldTypeName}.{_currentToken.Value}"; // 向后扩充
                        NextToken();
                        continue;
                    }
                    else if (peekToken.Type == TokenType.Identifier)   
                    {
                        // 尝试解析变量名称
                        fieldName = peekToken.Value; // 字段名称
                        NextToken(); // 消耗类型定义的最后一个Token
                        NextToken(TokenType.Semicolon); // 消耗字段名称，如果下一个Token如果不为分号，说明词法异常
                        break; 
                    }
                }
                var fieldType = DynamicObjectHelper.GetCacheType(fieldTypeName); // 查询是否在其它脚本中创建过类型
                fieldType = fieldType ?? fieldTypeName.ToTypeOfString(); // 如果没有定义过类型，则从c#运行时尝试获取类型
                if(fieldType is null)
                {
                    throw new Exception($"定义类型 {className} 时，解析成员 {fieldName} 失败，没有找到类型 {fieldTypeName}。");
                }
                classFields.Add(fieldName, fieldType);
                NextToken();  // 当前字段类型定义完毕，消耗";"
            }
            var classToken = _currentToken;
            classToken.Code = _lexer.GetCoreContent(coreStartRangeIndex); // 收集类型定义的代码。
            NextToken();  // 当前类型定义完毕，消耗"}"

            // 词法分析时不应该获取类型，类型应该放在类型分支中进行追踪
            var type = DynamicObjectHelper.GetCacheType(className);
            if(type is null) // 解析时创建类型，因为后续的类型定义中，某个字段可能使用该类型，而解析时需要获取到其类型
            {
                DynamicObjectHelper.CreateTypeWithProperties(classFields, className); 
            }

            var classTypeDefinitionNode = new ClassTypeDefinitionNode(classFields, className);
            
            classTypeDefinitionNode.SetTokenInfo(classToken);
            return classTypeDefinitionNode;
        }

        /// <summary>
        /// 解析 return 语句。
        /// </summary>
        /// <returns></returns>
        public ReturnNode ParseReturnNode()
        {
            var returnToken = _currentToken;
            if (JudgmentKeyword("return")) throw new Exception($"解析返回节点时，当前Token不为关键字“return”。");
            NextToken(); // 消耗关键字
            ReturnNode returnNode;
            if (_currentToken.Type == TokenType.Semicolon)
            {
                returnNode = new ReturnNode();  // 空返回
            }
            else
            {
                var resultValue = BooleanExpression(); // 解析值表达式
                returnNode = new ReturnNode(resultValue); // 带值返回
            }
            returnNode.SetTokenInfo(returnToken);
            if (_currentToken.Type == TokenType.Semicolon)
            {
                NextToken(); // 消耗 ";" ，语句完毕
            }
            return returnNode;
        }

        /// <summary>
        /// （不处理分号）解析对象实例化行为
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public ObjectInstantiationNode ParseObjectInstantiationNode()
        {

            // 获取类型名称，类型允许包含逗号（使用C#中的类型）
            /*
             new [TypeName]([Exp1],[Exp2]);
            类型解析时的预期顺序：
                    1     2      3     4      5        6
            new -> [id -> dot -> id -> dot -> id] -> ParenthesisLeft -> [CtroArg] -> ParenthesisRight -> Semicolon 
                   [         类型定义           ]       括号 "("         构造入参           括号 ")"      分号，解析完成
             */
            var instantiationToken = _currentToken;
            // 第一次进入循环时，会消耗 new 关键字
            NextToken(TokenType.Identifier);
            string typeName = _currentToken.Value; // 类名
            List<ASTNode> ctorArguments = []; // 构造入参
            while (true)
            {
               
                var peekToken = _lexer.PeekToken();
                if (peekToken.Type == TokenType.Dot)
                {
                    
                    typeName = $"{typeName}.{_currentToken.Value}"; // 向后扩充
                    NextToken(); 
                    continue;
                }
                else if (peekToken.Type == TokenType.ParenthesisLeft)
                {
                    // 类名定义完成，解析构造方法入参。
                    NextToken(); // 消耗 类型Token
                    NextToken(); // 消耗 "("
                    while (_currentToken.Type != TokenType.ParenthesisRight) // 表示参数获取完毕 
                    {
                        ASTNode node = ParserArgNode(); // 解析构造方法的入参
                        ctorArguments.Add(node);
                        if(_currentToken.Type == TokenType.Comma)
                        {
                            NextToken(); // 消耗参数分隔符 "," 
                        }
                    }
                    NextToken(); // 消耗 ")"
                    break;
                }
            }
            // NextToken(TokenType.Semicolon); // 消耗字段名称，如果下一个Token如果不为分号，说明词法异常
            ObjectInstantiationNode objectInstantiationNode = new ObjectInstantiationNode(typeName, ctorArguments);
            objectInstantiationNode.SetTokenInfo(instantiationToken);
            return objectInstantiationNode; 
        }

        #region 表达式解析：BooleanExpression -> ComparisonExpression -> Expression -> Term -> Factor
        /// <summary>
        /// 解析表达式
        /// </summary>
        /// <returns></returns>
        private ASTNode ParserExpression()
        {
            // 处理表达式时，从布尔表达式开始。
            return BooleanExpression();
        }
        #region 表达式处理

        /// <summary>
        /// 布尔表达式
        /// </summary>
        /// <returns></returns>
        private ASTNode BooleanExpression()
        {
            ASTNode left = ComparisonExpression();

            while (_currentToken.Type == TokenType.Operator &&
                  (_currentToken.Value == "&&" || _currentToken.Value == "||"))
            {
                string op = _currentToken.Value;
                _currentToken = _lexer.NextToken();
                ASTNode right = ComparisonExpression();
                left = new BinaryOperationNode(left, op, right).SetTokenInfo(_currentToken);
            }

            return left;
        }

        /// <summary>
        /// 比较表达式（==, !=, <, <=, >, >=）
        /// </summary>
        /// <returns></returns>
        private ASTNode ComparisonExpression()
        {
            ASTNode left = Expression();

            while (_currentToken.Type == TokenType.Operator &&
                  (_currentToken.Value == "==" || _currentToken.Value == "!=" ||
                   _currentToken.Value == "<" || _currentToken.Value == "<=" ||
                   _currentToken.Value == ">" || _currentToken.Value == ">="))
            {
                string op = _currentToken.Value;
                _currentToken = _lexer.NextToken();
                ASTNode right = Expression();
                left = new BinaryOperationNode(left, op, right).SetTokenInfo(_currentToken);
            }

            return left;
        }

        /// <summary>
        /// 加减
        /// </summary>
        /// <returns></returns>
        private ASTNode Expression()
        {
            ASTNode left = Term();

            while (_currentToken.Type == TokenType.Operator &&
                  (_currentToken.Value == "+" || _currentToken.Value == "-"))
            {
                string op = _currentToken.Value;
                _currentToken = _lexer.NextToken();
                ASTNode right = Term();
                left = new BinaryOperationNode(left, op, right).SetTokenInfo(_currentToken);
            }

            return left;
        }

        /// <summary>
        /// 乘除
        /// </summary>
        /// <returns></returns>
        private ASTNode Term()
        {
            ASTNode left = Factor();
            while (_currentToken.Type == TokenType.Operator &&
                  (_currentToken.Value == "*" || _currentToken.Value == "/"))
            {
                string op = _currentToken.Value;
                _currentToken = _lexer.NextToken();
                ASTNode right = Factor();
                left = new BinaryOperationNode(left, op, right).SetTokenInfo(_currentToken);
            }
            
            return left;
        } 
        #endregion

        /// <summary>
        /// 解析因子（Factor），用于处理基本的字面量、标识符、括号表达式等。
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private ASTNode Factor()
        {
            /*

            表达式类型：
              1. 赋值语句中的右值       ... = [Exp];（左值不为表达式）
              2. 一般语句               [Exp];
              3. 方法中参数             func([Exp1],[Exp2],[Exp3]...) 单个入参
              3. 实例化类型             new Class();
            表达式特征：
              a. 在第 1 次分析中，当 _currentToken.Type 为 Identifier 时，下一个 Token 可能为：
                  1. TokenType.ParenthesisLeft  => 调用本地方法
                  2. TokenType.Dot              => 当前 Toke 代表变量，需要获取对象成员
                  3. TokenType.ParenthesisLeft  => 当前 Token 表示数组对象，索引为 PeekToken(2)，根据索引获取成员
                  4. TokenType.Keyword("new")   => 实例化类型
                  5. TokenType.Semicolon        => 表达式结束
              b. 在第 N + 1 次分析中，当 _currentToken.Type 为 Identifier 时，下一个 Token 可能为：
                  1. TokenType.ParenthesisLeft  => 以上一次分析得到的 Node 为 Source Object，调用其的方法。
                  2. TokenType.Dot              => 以上一次分析得到的 Node 为 Source Object，需要获取对象成员
                  3. TokenType.ParenthesisLeft  => 以上一次分析得到的 Node 为 Source Object，获取 _currentToken.Value 对应的数组成员，索引为 PeekToken(2)，根据索引获取成员
                  4. TokenType.Keyword("new")   => 实例化类型
                  5. TokenType.Semicolon        => 表达式结束
              c. 当 _currentToken.Type 为 ParenthesisLeft 时 ：
                  存在嵌套表达式，再次调用 ParserExpression() 方法。
                 
            */

            // 标识符节点
            var factorToken = _currentToken; // 记录一下进入 Factor() 的 Token
            if (_currentToken.Type == TokenType.Identifier) 
            {
                /*
                    表达式获取 Expression() 
                    variable;               | 变量  √
                    value;                  | 显式设置的字面量。 √
                    obj.Value...;           | obj为之前的上下文中出现过的变量，调用表达式包含对象成员数组、方法
                    array[...];             | array为之前的上下文中出现过的变量。
                    array[...].Value...;    | array为之前的上下文中出现过的变量。调用表达式包含对象成员数组、方法
                    new Class(...);         | 实例化类型，包含构造函数 √
                 */
                var backupToken = _currentToken;
                //var tempPeekToken = _lexer.PeekToken();
                var targetNode = new IdentifierNode(_currentToken.Value).SetTokenInfo(_currentToken); // 获取变量
                var peekToken2 = _lexer.PeekToken(); // 消耗 第一个标识符
                
                if (peekToken2.Type == TokenType.Semicolon
                    || peekToken2.Type == TokenType.ParenthesisRight
                    || peekToken2.Type == TokenType.SquareBracketsRight
                    || peekToken2.Type == TokenType.Comma
                    || peekToken2.Type == TokenType.Operator
                    )
                {
                    NextToken(); // 消耗标识符
                    return targetNode;
                }
                if (peekToken2.Type == TokenType.ParenthesisLeft)
                {
                    // 解析调用挂载方法
                    // ... = variable()()();
                    // 暂时不支持柯里化调用... = variable()()();
                    var functionCallNode = ParseFunctionCallNode();
                    if(_currentToken.Type == TokenType.Semicolon)
                    {
                        return functionCallNode;
                    }
                    targetNode = functionCallNode;
                }
                else if (peekToken2.Type == TokenType.SquareBracketsLeft)
                {
                    // 解析集合获取
                    var collectionIndexNode = ParseCollectionIndexNode(targetNode);
                    if (_currentToken.Type == TokenType.Semicolon)
                    {
                        return collectionIndexNode;
                    }
                    targetNode = collectionIndexNode;
                }else if (peekToken2.Type == TokenType.Dot)
                {
                    NextToken(); // 消耗标识符
                }

                // 开始解析
                List<ASTNode> nodes = [targetNode];
                ASTNode? source;
                while (true)
                {
                    var peekToken = _currentToken; // _lexer.PeekToken(); // 获取下一个token开始判断
                    source = nodes[^1]; // 重定向节点
                    if (peekToken.Type == TokenType.Dot) // 从对象获取
                    {
                        /*
                          1. 获取成员      => PeekToken.Type = TokenType.Dot
                          2. 获取集合      => PeekToken.Type = TokenType.SquareBracketsLeft
                          3. 调用成员方法  => PeekToken.Type = TokenType.ParenthesisLeft
                         */
                        NextToken(); // 消耗 "." 并获取下一个成员。
                        var peekToken3 = _lexer.PeekToken();
                        ASTNode tempNode = peekToken3.Type switch
                        {
                            TokenType.Dot => ParseMemberAccessNode(source), // 获取对象中的成员 source.Value...
                            TokenType.Semicolon => ParseMemberAccessNode(source), // 获取对象中的成员 source.Value...
                            TokenType.ParenthesisRight => ParseMemberAccessNode(source), // 获取对象中的成员 source.Value...
                            TokenType.SquareBracketsLeft => ParseCollectionIndexNode(source), // 获取集合中的元素 source[index]....
                            TokenType.ParenthesisLeft => ParseMemberFunctionCallNode(source), // 获取需要调用的方法 source(arg1,arg2...)...
                            _ => throw new Exception($"无法从对象获取成员，当前Token类型为 {peekToken.Type}。")
                        };
                        nodes.Add(tempNode);
                        continue; // 结束当前轮次的token判断

                    }
                    else if (peekToken.Type == TokenType.ParenthesisLeft) // 调用对象方法
                    {
                        var memberFunctionCallNode = ParseMemberFunctionCallNode(source);
                        nodes.Add(memberFunctionCallNode);
                        continue; // 结束当前轮次的token判断

                    }
                    else if (peekToken.Type == TokenType.SquareBracketsLeft) // 集合获取
                    {
                        var collectionIndexNode = ParseCollectionIndexNode(source);
                        nodes.Add(collectionIndexNode);
                        continue; // 结束当前轮次的token判断
                    }
                    else if (peekToken.Type == TokenType.Semicolon)
                    {
                        break;
                    }
                    else
                    {
                        if(peekToken.Type == TokenType.ParenthesisRight // 可能解析完了方法参数
                            || peekToken.Type == TokenType.Comma // 可能解析完了方法参数
                            || peekToken.Type == TokenType.ParenthesisRight) // 可能解析完了下标索引
                        {
                            return source;
                        }
                        // 应该是异常，如果是其它符号，说明词法解析不符合预期
                        throw new Exception($"在 Expression().Factor() 遇到意外的 Token: [{_currentToken.Type}]\"{_currentToken.Value}\" ");
                       
                    }
                }
                return source;

            }
            else if (_currentToken.Type == TokenType.ParenthesisLeft)  // 嵌套表达式
            {
                NextToken();   // 消耗 "("
                _currentToken = _lexer.NextToken();  // 消耗 "("
                var expNode = BooleanExpression();
                if (_currentToken.Type != TokenType.ParenthesisRight)
                    throw new Exception($"解析嵌套表达式时遇到非预期的符号 \"{_currentToken.Type}\"，预期符号为\")\"。");
                NextToken();  // 消耗 ")"
                return expNode;
            }
            else if(_currentToken.Type == TokenType.Keyword && _currentToken.Value == "new") // 创建对象
            {
                return ParseObjectInstantiationNode();
            }

            #region 字面量因子
            else if (_currentToken.Type == TokenType.Null)
            {
                NextToken(); // 消耗 null
                return new NullNode().SetTokenInfo(factorToken);
            }
            else if (_currentToken.Type == TokenType.Boolean)
            {
                var value = bool.Parse(_currentToken.Value);
                NextToken();   // 消耗布尔量
                return new BooleanNode(value).SetTokenInfo(factorToken);
            }
            else if (_currentToken.Type == TokenType.String)
            {
                var value = _currentToken.Value;
                NextToken();  // 消耗字符串
                var node = new StringNode(value).SetTokenInfo(factorToken);
                return node;
            }
            else if (_currentToken.Type == TokenType.Char)
            {
                var value = _currentToken.Value;
                NextToken(); ;  // 消耗Char
                return new CharNode(value).SetTokenInfo(factorToken);
            }
            else if (_currentToken.Type == TokenType.InterpolatedString)
            {
                // 暂未实现插值字符串
                // 可能是插值字符串；
                // let context = $"a{A}b{B}c";
                // let context = "a" + A + "b" + B + c;
                NextToken(); // 消耗字符串
                while (_currentToken.Type == TokenType.String)
                {
                }
            }
            else if (_currentToken.Type == TokenType.NumberInt)
            {
                var value = int.Parse(_currentToken.Value);
                NextToken();  // 消耗 int 整型
                return new NumberIntNode(value).SetTokenInfo(factorToken);
            }
            else if (_currentToken.Type == TokenType.NumberLong)
            {
                var value = long.Parse(_currentToken.Value);
                NextToken();  // 消耗  long 整型
                return new NumberLongNode(value).SetTokenInfo(factorToken);
            }
            else if (_currentToken.Type == TokenType.NumberFloat)
            {
                var value = float.Parse(_currentToken.Value);
                NextToken();  // 消耗 float 浮点数
                return new NumberFloatNode(value).SetTokenInfo(factorToken);
            }
            else if (_currentToken.Type == TokenType.NumberDouble)
            {
                var value = float.Parse(_currentToken.Value);
                NextToken();  // 消耗 double 浮点数
                return new NumberDoubleNode(value).SetTokenInfo(factorToken);
            }
            #endregion


            throw new Exception($"在 Expression().Factor() 遇到意外的 TokenType ,{_currentToken.Type} {_currentToken.Value} ");
        }
        #endregion

        #region 辅助方法

        private Token NextToken()
        {
            _currentToken =  _lexer.NextToken();
            return _currentToken;
        }
        
        private Token NextToken(TokenType expectationTokenType)
        {
            _currentToken =  _lexer.NextToken();
            if(_currentToken.Type != expectationTokenType)
            {
                throw new Exception($"分析器获取下一个Token时，期望获取 {expectationTokenType} ，但实际获取 {_currentToken.Type}。" +
                    $"Value      : {_currentToken.Value}" +
                    $"StartIndex : {_currentToken.StartIndex}" +
                    $"Length     : {_currentToken.Length}" +
                    $"");
            }
            return _currentToken;
        }

        /// <summary>
        /// 判断当前Token操作符类型
        /// </summary>
        /// <param name="operator"></param>
        /// <returns></returns>
        private bool JudgmentTokenType(TokenType tokenType)
        {
            return _currentToken.Type == tokenType;
        }

        /// <summary>
        /// 判断Token操作符类型
        /// </summary>
        /// <param name="operator"></param>
        /// <returns></returns>
        private bool JudgmentOperator(Token peekToken, string @operator)
        {
            if (peekToken.Type != TokenType.Operator)
            {
                return false;
            }
            return peekToken.Value == @operator;
        }

        /// <summary>
        /// 判断当前Token关键字类型
        /// </summary>
        /// <param name="operator"></param>
        /// <returns></returns>
        private bool JudgmentKeyword(string keyword)
        {
            if (_currentToken.Type != TokenType.Keyword)
            {
                return false;
            }
            return _currentToken.Value == keyword;
        }



        #endregion



        #region 废弃的解析方法
        /*


        /// <summary>
        /// 解析单个语句。
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private ASTNode Statement()
        {

            while (_currentToken.Type == TokenType.Semicolon)
            {
                _currentToken = _lexer.NextToken();
            }
            if (_currentToken.Type == TokenType.EOF)
            {
                return null;
            }
            if (_currentToken.Type == TokenType.Identifier)
            {
                // 处理标识符，可能是函数调用、变量赋值或对象成员访问等行为
                return ParseIdentifier();
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
            
            if (_currentToken.Type == TokenType.Null)
            {
                // 处理 null 语句
                return BooleanExpression();
            }

            *//**//*
            // 处理其他语句（如表达式语句等）
            if (_currentToken.Type == TokenType.Semicolon)
            {
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
            *//*
             localFunc();
             obj.Func();
             obj.Value = ...;
             value = ...;
             *//*
            var backupToken = _currentToken;
            var tokenCount = 1;
            var int_type = 0;
            int_type = GetFlowType(_lexer, ref tokenCount);
            if (int_type == 1) // 赋值 MemberAssignmentNode
            {
                //_lexer.SetToken(backupToken);
                var objectName = _currentToken.Value;
                var objectNode = new IdentifierNode(objectName).SetTokenInfo(_currentToken); // 首先定义对象变量节点
                var peekToken = _lexer.PeekToken();
                if (peekToken.Type == TokenType.Operator && peekToken.Value == "=")
                {
                    // 变量赋值
                    _currentToken = _lexer.NextToken(); // 消耗 变量名
                    _currentToken = _lexer.NextToken(); // 消耗 “=”

                    var valueNode = BooleanExpression();
                    var assignmentNode = new AssignmentNode(objectNode, valueNode).SetTokenInfo(_currentToken);
                    return assignmentNode;
                }

                _currentToken = _lexer.NextToken(); // 消耗对象名称
                                                    //_lexer.SetToken(backupToken);

                List<ASTNode> nodes = new List<ASTNode>(); // 表达对象成员路径的节点
                while (true)
                {
                    if (_currentToken.Type != TokenType.Dot)
                    {
                        _currentToken = _lexer.NextToken();
                        continue;
                    }

                    _currentToken = _lexer.NextToken();  // 消耗 "."  获取下一个成员
                    if (_currentToken.Type != TokenType.Identifier)
                    {
                        continue;
                        //_lexer.SetToken(peekToken); // 重置lexer
                    }
                    var temp2token = _lexer.PeekToken();
                    var sourceNode = nodes.Count == 0 ? objectNode : nodes[^1];
                    if (temp2token.Type == TokenType.ParenthesisLeft)
                    {
                        // 解析方法调用 obj.func()
                        ASTNode functionNode = ParseMemberFunctionCall(sourceNode).SetTokenInfo(_currentToken);
                        nodes.Add(functionNode);
                    }
                    else if (temp2token.Type == TokenType.SquareBracketsLeft)
                    {
                        // 成员数组 obj.dict[key] / obj.array[index]
                        if (_currentToken.Type == TokenType.Identifier)
                        {
                            var memberAccessNode = new MemberAccessNode(sourceNode, _currentToken.Value).SetTokenInfo(_currentToken);
                            sourceNode = memberAccessNode;
                        }
                        var coolectionNode = ParseCollectionIndex(sourceNode).SetTokenInfo(_currentToken);
                        nodes.Add(coolectionNode);
                    }
                    else if (temp2token.Type is TokenType.Dot *//* or TokenType.ParenthesisRight*//*)
                    {
                        // 成员获取 obj.value
                        var memberAccessNode = new MemberAccessNode(sourceNode, _currentToken.Value).SetTokenInfo(_currentToken);
                        nodes.Add(memberAccessNode);
                    }
                    else if (temp2token.Type == TokenType.Operator && temp2token.Value == "=")
                    {
                        //  左值结束， 成员获取 obj.value
                        *//*  var memberAccessNode = new MemberAccessNode(sourceNode, _currentToken.Value).SetTokenInfo(_currentToken);
                         nodes.Add(memberAccessNode);*//*
                        var memberName = _currentToken.Value; // 成员名称
                        _currentToken = _lexer.NextToken(); // 消耗 成员 token
                        _currentToken = _lexer.NextToken(); // 消耗“=” 等号
                        var rightNode = BooleanExpression(); // 判断token
                        MemberAssignmentNode assignmentNode = new MemberAssignmentNode(sourceNode, memberName, rightNode);
                        assignmentNode.SetTokenInfo(_currentToken);

                        return assignmentNode; // 返回节点
                    }
                }
            }
            else if (int_type == 2) // 变量数组赋值
            {
                var objectName = _currentToken.Value;
                var objectNode = new IdentifierNode(objectName).SetTokenInfo(_currentToken); // 首先定义对象变量节点
                _currentToken = _lexer.NextToken(); // 消耗 变量名称
                if (_currentToken.Type != TokenType.SquareBracketsLeft)
                    throw new Exception("数组需要 '[' 符号");
                _currentToken = _lexer.NextToken(); // 消耗 "["
                ASTNode indexValue = BooleanExpression(); // 获取表达数组下标的节点

                _currentToken = _lexer.NextToken(); // 消耗 "]"
                var collectionNode = new CollectionIndexNode(objectNode, indexValue);
                collectionNode.SetTokenInfo(_currentToken); // 集合节点

                if (!(_currentToken.Type == TokenType.Operator && _currentToken.Value == "="))
                    throw new Exception("数组赋值需要 '=' 符号");
                _currentToken = _lexer.NextToken(); // 消耗 "="

                var valueNode = BooleanExpression(); // 获取右值表达式

                var assignmentNode = new CollectionAssignmentNode(collectionNode, valueNode).SetTokenInfo(_currentToken); // 集合节点;
                return assignmentNode;
                var tempToken1 = _lexer.PeekToken(1);
                var tempToken2 = _lexer.PeekToken(2);
                var tempToken3 = _lexer.PeekToken(3);
                _currentToken = _lexer.NextToken();
                // object[1] = "";
                var taretNode = "";
            }
            *//* else if (int_type == 3) // 方法调用
             {
                 var taretNode = "";
             }*//*
            else if (int_type == 4) // 方法调用
            {
                // 可能是挂载函数调用
                var functionCallNode = ParseFunctionCall();
                return functionCallNode;
            }
            else
            {

            }


            int GetFlowType(SereinScriptLexer _lexer, ref int tokenCount)
            {
                int int_type;
                while (true)
                {
                    var tempToken = _lexer.PeekToken(tokenCount++);

                    if (tempToken.Type == TokenType.Operator && tempToken.Value == "=")
                    {
                        var tempToken2 = _lexer.PeekToken(tokenCount - 2);
                        if (tempToken2.Type == TokenType.SquareBracketsRight)
                        {
                            int_type = 2;   // 变量数组赋值
                            break;
                        }
                        else // if (tempToken2.Type == TokenType.SquareBracketsRight)
                        {
                            int_type = 1; // 变量赋值
                            break;
                        }
                    }
                    *//* if (tempToken.Type == TokenType.Semicolon)
                     {
                         var tempToken2 = _lexer.PeekToken(tokenCount);
                         if(tempToken2.Type == TokenType.ParenthesisRight)
                         {
                             int_type = 3; // 方法调用
                             break;
                         }
                     }*//*
                    if (tempToken.Type == TokenType.ParenthesisLeft) // 本地函数调用
                    {
                        int_type = 4; // 本地方法调用
                        break;
                    }

                }

                return int_type;
            }

            return null;
            #region MyRegion
            *//*return null;
                var _identifierPeekToken = _lexer.PeekToken();
                if (_identifierPeekToken.Type == TokenType.Operator && _identifierPeekToken.Value == "=")
                {
                    // 如果是操作符 = ，则是直接赋值变量
                    var leftValueNode = new IdentifierNode(_currentToken.Value).SetTokenInfo(_currentToken);
                    var rightValueNode = BooleanExpression();
                    return new AssignmentNode(leftValueNode, rightValueNode).SetTokenInfo(_currentToken);

                    //return new IdentifierNode(_currentToken.Value).SetTokenInfo(_currentToken); // 获取变量
                }
                if (_identifierPeekToken.Type == TokenType.ParenthesisLeft)
                {
                    // 可能是挂载函数调用
                    var functionCallNode = ParseFunctionCall();
                    return functionCallNode;
                }
                var objToken = _currentToken; // 对象Token
                ASTNode? objectNode = new IdentifierNode(objToken.Value).SetTokenInfo(objToken); // 对象节点
                var identifier = _currentToken.Value; // 标识符字面量
                List<ASTNode> nodes = new List<ASTNode>();
                while (true)
                {
                    if (_currentToken.Type == TokenType.Dot)
                    {
                        _currentToken = _lexer.NextToken();
                        if (_currentToken.Type == TokenType.Identifier)
                        {
                            var temp2token = _lexer.PeekToken();
                            var sourceNode = (nodes.Count == 0 ? objectNode : nodes[^1]);
                            if(temp2token.Type == TokenType.Operator && temp2token.Value == "=")
                            {
                                // 成员获取 obj.value = 
                                var memberAccessNode = new MemberAccessNode(sourceNode, _currentToken.Value).SetTokenInfo(_currentToken);
                                nodes.Add(memberAccessNode);
                                //break;
                                // 赋值行为
                                *//*var assignmentNode = ParseAssignment();
                                nodes.Add(assignmentNode);*//*
                            }
                            else if (temp2token.Type == TokenType.ParenthesisLeft)
                            {
                                // 解析方法调用 obj.func()
                                ASTNode functionNode = ParseMemberFunctionCall(sourceNode).SetTokenInfo(_currentToken);
                                nodes.Add(functionNode);
                            }
                            else if (temp2token.Type == TokenType.SquareBracketsLeft)
                            {
                                // 成员数组 obj.dict[key] / obj.array[index]
                                if (_currentToken.Type == TokenType.Identifier)
                                {
                                    var memberAccessNode = new MemberAccessNode(sourceNode, _currentToken.Value).SetTokenInfo(_currentToken);
                                    sourceNode = memberAccessNode;
                                }
                                var coolectionNode = ParseCollectionIndex(sourceNode).SetTokenInfo(_currentToken);
                                nodes.Add(coolectionNode);
                            }
                            else if (temp2token.Type is TokenType.Dot or TokenType.Semicolon or TokenType.ParenthesisRight )
                            {
                                // 成员获取 obj.value
                                var memberAccessNode = new MemberAccessNode(sourceNode, _currentToken.Value).SetTokenInfo(_currentToken);
                                nodes.Add(memberAccessNode);
                            }
                            //_lexer.SetToken(peekToken); // 重置lexer
                        }
                    }
                    else
                    {
                        _currentToken = _lexer.NextToken();
                    }
                    if (nodes.Count > 0 && _currentToken.Type == TokenType.Operator && _currentToken.Value == "=")
                    {
                        _currentToken = _lexer.NextToken(); // 消耗操作符
                        // 分号意味着语句结束
                        var rightValueNode = BooleanExpression();
                        if (nodes.Count == 1)
                        {
                            //_currentToken = _lexer.NextToken(); // 右值
                            var assignmentNode = new AssignmentNode(nodes[0], rightValueNode);
                            //var node = nodes[^1];
                            return assignmentNode;
                        }
                        else
                        {
                            var objNode = new ObjectMemberExpressionNode(objectNode, nodes).SetTokenInfo(objToken);
                            var assignmentNode = new AssignmentNode(objNode, rightValueNode);
                            return objNode;
                        }
                        //AssignmentNode assignmentNode = new AssignmentNode(rightValueNode);
                        break;
                    }
                    if (_currentToken.Type is TokenType.Semicolon)
                    {
                        // 分号意味着语句结束
                        break;
                    }
                }
                if (nodes.Count == 0)
                {
                    return objectNode;
                }
                if (nodes.Count == 1)
                {
                    var t = new AssignmentNode(objectNode, nodes[^1]);
                    var node = nodes[^1];
                    return node;
                }
                else
                {
                    var objNode = new ObjectMemberExpressionNode(objectNode, nodes).SetTokenInfo(objToken);
                    return objNode;
                }*//*
            #endregion

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
                    valueNode = BooleanExpression();
                    //valueNode = Expression();
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
                    valueNode = BooleanExpression();
                    //valueNode = Expression();
                }
                var variableNode = new IdentifierNode(variableName).SetTokenInfo(_currentToken);
                return new AssignmentNode(variableNode, valueNode).SetTokenInfo(_currentToken);
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
            string variableName = _currentToken.Value.ToString(); // 变量名称
            _currentToken = _lexer.NextToken(); // Consume identifier
            ASTNode value;
            AssignmentNode assignmentNode;
            if (_currentToken.Type == TokenType.Semicolon)
            {
                // 定义一个变量，初始值为 null
                value = new NullNode();
                var variableNode = new IdentifierNode(variableName).SetTokenInfo(_currentToken);
                assignmentNode = new AssignmentNode(variableNode, value); // 生成node
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
                value = BooleanExpression(); // 解析获取赋值表达式
                                             //value = Expression(); // 解析获取赋值表达式
                var variableNode = new IdentifierNode(variableName).SetTokenInfo(_currentToken);
                assignmentNode = new AssignmentNode(variableNode, value); // 生成node
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
            *//*
             * 有两种定义类型的方式：
             *   1. class MyClass{}
             *   2. new class MyClass{} 
             * 解析执行时，第二种方式定义的类，会顶掉其他地方创建的“MyClass”同名类型；
             *//*
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
                var fieldTypeName = _currentToken.Value.ToString();
                var dynamicType = DynamicObjectHelper.GetCacheType(fieldTypeName);
                
                var fieldType = dynamicType ?? _currentToken.Value.ToString().ToTypeOfString(); // 获取字段的类型
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
            var typeDefinitionCode = _lexer.GetCoreContent(coreStartRangeIndex); // 收集类型定义的代码。
           
            DynamicObjectHelper.CreateTypeWithProperties(classFields, className, isOverlay); // 解析时缓存类型
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
                //arguments.Add(Expression()); // 获取参数表达式
                arguments.Add(BooleanExpression()); // 获取参数表达式
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
            _currentToken = _lexer.NextToken(); // 消耗数组名称 identifier
            // ParenthesisLeft
            if (_currentToken.Type != TokenType.SquareBracketsLeft)
                throw new Exception("数组操作需要 '[' 符号");

            _currentToken = _lexer.NextToken(); // 消耗 "["

            //ASTNode indexValue = Expression(); // 获取表达数组下标的节点
            ASTNode indexValue = BooleanExpression(); // 获取表达数组下标的节点

            _currentToken = _lexer.NextToken(); // 消耗 "]"
            return new CollectionIndexNode(identifierNode, indexValue).SetTokenInfo(_currentToken);
        }
        
        /// <summary>
        /// 指定对象解析集合索引行为（数组或字典）
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public ASTNode ParseCollectionIndex(ASTNode ObjectNode)
        {
            string collectionName = _currentToken.Value.ToString();
            _currentToken = _lexer.NextToken(); // 消耗数组名称 identifier
            // ParenthesisLeft
            if (_currentToken.Type != TokenType.SquareBracketsLeft)
                throw new Exception("Expected '[' after function name");

            _currentToken = _lexer.NextToken(); // 消耗 "["

            //ASTNode indexValue = Expression(); // 获取表达数组下标的节点
            ASTNode indexValue = BooleanExpression(); // 获取表达数组下标的节点

            _currentToken = _lexer.NextToken(); // 消耗 "]"
            return new CollectionIndexNode(ObjectNode, indexValue).SetTokenInfo(_currentToken);
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
                    //var valueNode = Expression();  // 解析右值
                    var valueNode = BooleanExpression();  // 解析右值
                    _currentToken = _lexer.NextToken(); // 消耗 变量
                    //_currentToken = _lexer.NextToken(); // 消耗 ;
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
            _currentToken = _lexer.NextToken(); // 消耗函数名称

            if (_currentToken.Type != TokenType.ParenthesisLeft)
                throw new Exception("Expected '(' after function name");

            _currentToken = _lexer.NextToken(); // 消耗 "("

            var arguments = new List<ASTNode>();
            while (_currentToken.Type != TokenType.ParenthesisRight)
            {
                // 获取参数表达式
                //var arg = Expression();
                var arg = BooleanExpression();
                _currentToken = _lexer.NextToken(); // 消耗参数 arg
                arguments.Add(arg);  // 添加到参数列表
                if (_currentToken.Type == TokenType.Comma)
                {
                    _currentToken = _lexer.NextToken(); // 消耗参数分隔符 ","
                }
                if (_currentToken.Type == TokenType.Semicolon)
                {
                    break; // 消耗 ";"
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

            bool isBreak = false;
            var arguments = new List<ASTNode>();
            // 获取参数
            while (true)// _currentToken.Type != TokenType.ParenthesisRight
            {
                if (_currentToken.Type == TokenType.ParenthesisRight)
                {
                    break;
                }
                //var arg = Expression(); // 获取参数表达式
                var arg = BooleanExpression(); // 获取参数表达式
                arguments.Add(arg);
                if (_currentToken.Type == TokenType.Comma)
                {
                    _currentToken = _lexer.NextToken(); // 消耗参数分隔符 ","
                }
                if (_currentToken.Type == TokenType.ParenthesisRight )
                {
                    _currentToken = _lexer.NextToken(); // 消耗方法结束括号 "）"
                }
                if (_currentToken.Type == TokenType.Semicolon )
                {
                    //isBreak = true;
                    break; // consume ";"
                }
                *//*if (_currentToken.Type == TokenType.Semicolon) // 提前结束
                {
                    arguments.Add(arg);
                    isBreak = true;
                    break; // consume ";"
                }*//*
                //_currentToken = _lexer.NextToken(); // consume arg
            }
            //if(!isBreak)
            //    _currentToken = _lexer.NextToken(); // consume ")"

           
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
            var resultValue = BooleanExpression(); // 获取返回值表达式
            //var resultValue = Expression(); // 获取返回值表达式
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
            ASTNode condition = BooleanExpression();
            //ASTNode condition = Expression();
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
                while (_currentToken.Type == TokenType.Semicolon)
                {
                    _currentToken = _lexer.NextToken();
                }
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
                    while (_currentToken.Type == TokenType.Semicolon)
                    {
                        _currentToken = _lexer.NextToken();
                    }
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
            ASTNode condition = BooleanExpression();
            //ASTNode condition = Expression();
            _currentToken = _lexer.NextToken(); // Consume ")"
            _currentToken = _lexer.NextToken(); // Consume "{"
            List<ASTNode> body = new List<ASTNode>();
            while (_currentToken.Type != TokenType.BraceRight)
            {
                var node = Statement();
                if(node is not null)
                {
                    body.Add(node); // 解析循环体中的语句
                }
                if (_currentToken.Type == TokenType.Semicolon)
                {
                    _currentToken = _lexer.NextToken(); // Consume ";"
                }

                
            }
            _currentToken = _lexer.NextToken(); // Consume "}"
            return new WhileNode(condition, body).SetTokenInfo(_currentToken);
        }






        /// <summary>
        /// 解析因子（Factor），用于处理基本的字面量、标识符、括号表达式等。
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private ASTNode FactorTemp()
        {
            // 标识符节点
            if (_currentToken.Type == TokenType.Identifier)
            {
                var _identifierPeekToken = _lexer.PeekToken();
                if (_identifierPeekToken.Type is (TokenType.Dot and TokenType.ParenthesisLeft) 
                        or TokenType.Semicolon 
                        or TokenType.Comma
                        or TokenType.SquareBracketsRight) // 数组下标的"]"，应该是正在获取数组索引
                {
                    // 不是 "." 号，也不是 "(" , 或是";"，则是获取变量
                    var node = new IdentifierNode(_currentToken.Value).SetTokenInfo(_currentToken); // 获取变量
                    _currentToken = _lexer.NextToken(); // 消耗变量
                    return node;
                }

                if (_identifierPeekToken.Type == TokenType.SquareBracketsLeft)
                {
                    // 数组下标语法
                    //var functionCallNode = ParseFunctionCall();
                    var sourceNode = new IdentifierNode(_currentToken.Value).SetTokenInfo(_currentToken); // 获取变量
                    var coolectionNode = ParseCollectionIndex(sourceNode).SetTokenInfo(_currentToken);
                    return coolectionNode;
                }
                if (_identifierPeekToken.Type == TokenType.ParenthesisLeft)
                {
                    // 可能是挂载函数调用
                    var functionCallNode = ParseFunctionCall();
                    return functionCallNode;
                }
                var objToken = _currentToken; // 对象Token
                ASTNode? objectNode = new IdentifierNode(objToken.Value).SetTokenInfo(objToken); // 对象节点
                var identifier = _currentToken.Value; // 标识符字面量
                List<ASTNode> nodes = new List<ASTNode>();
                while (true)
                {
                    if (_currentToken.Type == TokenType.Dot)
                    {
                        _currentToken = _lexer.NextToken();
                        if (_currentToken.Type == TokenType.Identifier)
                        {
                            var temp2token = _lexer.PeekToken();
                            var sourceNode = (nodes.Count == 0 ? objectNode : nodes[^1]);
                            if (temp2token.Type == TokenType.ParenthesisLeft)
                            {
                                // 解析方法调用 obj.func()
                                ASTNode functionNode = ParseMemberFunctionCall(sourceNode).SetTokenInfo(_currentToken);
                                nodes.Add(functionNode);
                            }
                            else if (temp2token.Type == TokenType.SquareBracketsLeft)
                            {
                                // 成员数组 obj.dict[key] / obj.array[index]
                                if (_currentToken.Type == TokenType.Identifier)
                                {
                                    sourceNode = new MemberAccessNode(sourceNode, _currentToken.Value).SetTokenInfo(_currentToken);
                                }
                                var coolectionNode = ParseCollectionIndex(sourceNode).SetTokenInfo(_currentToken);
                                nodes.Add(coolectionNode);
                            }
                            else if (temp2token.Type is TokenType.Operator or TokenType.Dot or TokenType.Semicolon or TokenType.ParenthesisRight)
                            {
                                // 成员获取 obj.value
                                var memberAccessNode = new MemberAccessNode(sourceNode, _currentToken.Value).SetTokenInfo(_currentToken);
                                nodes.Add(memberAccessNode);
                            }
                            //_lexer.SetToken(peekToken); // 重置lexer
                        }
                    }
                    else
                    {
                        _currentToken = _lexer.NextToken();
                    }
                    if (_currentToken.Type is TokenType.Operator)
                    {
                        // 分号意味着语句结束
                        break;
                    }if (_currentToken.Type is TokenType.Semicolon)
                    {
                        // 分号意味着语句结束
                        break;
                    }
                }
                if (nodes.Count == 0)
                {
                    return objectNode;
                }
                else
                {
                    var node = nodes[^1];
                    var objNode = new ExpressionNode(node).SetTokenInfo(objToken);
                    return objNode;
                }
            }

            // 嵌套表达式
           if (_currentToken.Type == TokenType.ParenthesisLeft)
            {
                _currentToken = _lexer.NextToken();  // 消耗 "("
                var expr = BooleanExpression();
                //var expr = Expression();
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
            if (_currentToken.Type == TokenType.InterpolatedString)
            {
                // 暂未实现插值字符串
                // 可能是插值字符串；
                // let context = $"a{A}b{B}c";
                // let context = "a" + A + "b" + B + c;
                _currentToken = _lexer.NextToken(); // 消耗字符串
                while (_currentToken.Type == TokenType.String)
                {
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


            throw new Exception($"在Expression().Factor()遇到意外的 Token Type ,{_currentToken.Type} {_currentToken.Value} " );
        }*/ 
        #endregion
    }

}
