using Serein.Library.Utils;
using Serein.Script.Node;
using Serein.Script.Node.FlowControl;
using System.Collections;

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


        /// <summary>
        /// 命名空间
        /// </summary>
        public List<string> UsingNamespaces { get; } = new List<string>();

        /// <summary>
        /// 程序节点
        /// </summary>
        public List<ASTNode> Statements {  get; } = new List<ASTNode>();

        /// <summary>
        /// 解析整个程序，直到遇到文件结尾（EOF）为止。
        /// </summary>
        /// <returns></returns>
        private ProgramNode Program()
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

        private  ASTNode Statement()
        {
            if (_currentToken.Type == TokenType.Semicolon)
            {
                NextToken(); // 消耗 ";"
                return null;
            }

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
                    if(peekCount > 3999)
                    {
                        throw new Exception("解析异常，peek次数过多，可能是解析器出了bug");
                    }
                }
                #endregion

                #region 生成赋值语句/一般语句的ASTNode
                if (isAssignment)
                {
                    // 以赋值语句的形式进行处理
                    var assignmentNode = ParseAssignmentNode(); // 解析复制表达式
                    //if(_currentToken.Type == TokenType.Semicolon)
                        NextToken();// 消耗 ";"
                    return assignmentNode;
                }
                else
                {
                    // 以一般语句的形式进行处理，可当作表达式进行解析
                    var targetNode = ParserExpression();
                    //if (_currentToken.Type == TokenType.Semicolon) 
                        NextToken();// 消耗 ";"
                    return targetNode;
                } 
                #endregion

            }
            else if (JudgmentKeyword("using"))  // 定义对象
            {
                var usingNode = ParseUsingNode();
                UsingNamespaces.Add(usingNode.Namespace);
                return usingNode;
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
                if (JudgmentKeyword("if"))
                {
                    return ParseIf();
                }
                else if (JudgmentKeyword("while"))
                {
                    return ParseWhile();
                }
            }

            throw new Exception($"解析异常。{_currentToken.ToString()}");
            /*else
            {
                // 为避免解析异常，除了 Statement() 方法以外，其它地方不需要对  TokenType.Semicolon 进行处理
                throw new Exception("解析异常， Statement() 方法以外的地方处理了 TokenType.Semicolon ");
            }*/
        }

        /// <summary>
        /// 解析表达式根节点
        /// </summary>
        /// <returns></returns>
        private ASTNode ParserExpRootNode()
        {
            var name = _currentToken.Value;
            foreach(var nsp in UsingNamespaces)
            {
                var fullName = $"{nsp}.{name}";
                var type = SereinScriptTypeAnalysis.GetTypeOfString(fullName);
                if (type is null)
                {
                    continue;
                }
                var typeNode = new TypeNode(fullName).SetTokenInfo(_currentToken);
                return typeNode;
            }
            return new IdentifierNode(_currentToken.Value).SetTokenInfo(_currentToken); 
        }

        /// <summary>
        /// 解析赋值语句
        /// </summary>
        /// <returns></returns>
        public ASTNode ParseAssignmentNode()
        {
            /* 解析赋值语句
              赋值语句：
                 目标对象       表达式获取
                 Target() | = | Expression()            |
                          | = | variable;               | (变量）
                          | = | value;                  | 显式设置的字面量。
                          | = | obj.Value...;           | obj为之前的上下文中出现过的变量，调用表达式包含对象成员数组、方法
                          | = | array[...];             | array为之前的上下文中出现过的变量。
                          | = | array[...].Value...;    | array为之前的上下文中出现过的变量。调用表达式包含对象成员数组、方法
                          | = | new Class(...);         | 实例化类型，包含构造函数
                          | = | [v1, v2, v3];           | 定义数组

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
            /* 获取成员的方式有三种：
             *    1. 获取成员      => PeekToken.Type = TokenType.Dot
             *    2. 获取集合      => PeekToken.Type = TokenType.SquareBracketsLeft
             *    3. 调用成员方法  => PeekToken.Type = TokenType.ParenthesisLeft
             *    
             * */
            //if (JudgmentOperator(_currentToken, "=")) break; // 退出
            //var tempPeekToken = _lexer.PeekToken();

            
            var targetNode = ParserExpRootNode(); // 生成 Tagget 标记节点
            //var targetNode = new IdentifierNode(_currentToken.Value).SetTokenInfo(_currentToken); // 生成 Tagget 标记节点
            
            List<ASTNode> nodes = [targetNode];
            ASTNode? source;
           var peekNextToken = _lexer.PeekToken(); // 预览下个标识符
            if (JudgmentOperator(peekNextToken, "="))
            {
                NextToken();
            }
            else 
            { 
                if (peekNextToken.Type == TokenType.ParenthesisLeft)
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
                else if (peekNextToken.Type == TokenType.SquareBracketsLeft)
                {
                    // 解析集合获取
                    var collectionIndexNode = ParseCollectionIndexNode(targetNode);
                    if (_currentToken.Type == TokenType.Semicolon)
                    {
                        return collectionIndexNode;
                    }
                    targetNode = collectionIndexNode;
                }
                else if (peekNextToken.Type == TokenType.Dot)
                {
                    NextToken();
                }

                nodes = [targetNode];
                // 开始解析
                while (true)
                {
                    if (JudgmentOperator(_currentToken, "=")) break; // 退出
                    var peekToken = _currentToken; // _lexer.PeekToken(); // 获取下一个token开始判断
                    source = nodes[^1]; // 重定向节点
                    if (peekToken.Type == TokenType.Identifier) throw new Exception($"无法从对象获取成员，当前Token类型为 {peekToken.Type}。");
                    if (peekToken.Type == TokenType.Dot) // 从对象获取
                    {
                        /* 
                         * 获取成员的方式有三种：
                         *     1. 获取成员      => PeekToken.Type = TokenType.Dot
                         *     2. 获取集合      => PeekToken.Type = TokenType.SquareBracketsLeft
                         *     3. 调用成员方法  => PeekToken.Type = TokenType.ParenthesisLeft
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
                             TokenType.Dot or TokenType.Semicolon or TokenType.ParenthesisRight =>
                                    ParseMemberAccessNode(source), // 获取对象中的成员 source.Value...
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
                    
                }
            }
            targetNode = nodes[^1];
            if (targetNode is FunctionCallNode or MemberFunctionCallNode)
            {
                throw new Exception($"赋值语句左值部分不允许为方法调用,{targetNode.Code}");
            }
            else
            {
                var c = _currentToken;
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
            if(_currentToken.Type == TokenType.SquareBracketsLeft)
            {
                // 集合中获取集合
                NextToken(); // 消耗 "[" 集合标识符的左中括号
                ASTNode indexNode = ParserExpression(); // 解析获取索引Node
                NextToken(); // 消耗 "]" 集合标识符的右中括号

                if (sourceNode is IdentifierNode)
                {
                    var collectionIndexNode = new CollectionIndexNode(sourceNode, indexNode);
                    collectionIndexNode.SetTokenInfo(collectionToken); // 表示获取集合第几个索引
                    return collectionIndexNode;
                }
                else
                {
                    var collectionIndexNode = new CollectionIndexNode(sourceNode, indexNode);
                    collectionIndexNode.SetTokenInfo(collectionToken); // 表示获取集合第几个索引
                    return collectionIndexNode;
                }
            }
            else
            {
                string collectionName = _currentToken.Value; // 集合名称
                NextToken(TokenType.SquareBracketsLeft); // 消耗集合名称
                NextToken(); // 消耗 "[" 集合标识符的左中括号
                ASTNode indexNode = ParserExpression(); // 解析获取索引Node
                NextToken(); // 消耗 "]" 集合标识符的右中括号

                if (sourceNode is IdentifierNode)
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
        /// （不处理分号，逗号，右括号）用于解析(...) / [...] 中的参数部分。终止条件是 "," 参数分隔符 、")" 方法入参终止符、"]" 数组定义终止符
        /// </summary>
        /// <returns></returns>
        public ASTNode ParserArgNode()
        {
            ASTNode node = ParserExpression(); // 解析参数
            // ParserExpression 会完全解析当前表达式，自动移动到下一个Token，所以需要在这里判断是否符合期望的TokenType
            if(_currentToken.Type == TokenType.Comma 
                || _currentToken.Type == TokenType.ParenthesisRight
                || _currentToken.Type == TokenType.SquareBracketsRight)
            {
                return node;
            }
            throw new Exception($"解析参数节点后，当前Token类型不符合预期，当前类型为 {_currentToken.Type}。");
        }

        /// <summary>
        /// （不处理分号）
        /// 1. 引入命名空间
        /// </summary>
        /// <returns></returns>
        private UsingNode ParseUsingNode()
        {
            var startIndex = _lexer.GetIndex(); // 从“using”开始锚定代码范围
            NextToken(); //  消耗“using”关键字，获取类名
            List<string> namespaceItems = [];
            while (true) // 遇到 ";" 时结束
            {
                string className = _currentToken.Value; // 命名空间
                namespaceItems.Add(className);
                if (_currentToken.Type == TokenType.Semicolon)
                {
                    break;
                }
                NextToken(); // 命名空间
            }
            var nsp = string.Join('.', namespaceItems);
            UsingNode usingNode = new UsingNode(nsp);
            var usingToken = _currentToken;
            usingToken.Code = _lexer.GetCoreContent(startIndex); // 收集类型定义的代码。
            usingNode.SetTokenInfo(usingToken);
            return usingNode;
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
                        NextToken();
                        NextToken(TokenType.Identifier);
                        fieldTypeName = $"{fieldTypeName}.{_currentToken.Value}"; // 向后扩充
                        continue;
                    }
                    else if (peekToken.Type == TokenType.SquareBracketsLeft)
                    {
                        NextToken(); // 消耗数组类型定义的 "["
                        NextToken(); // 消耗数组类型定义的 "]"
                        fieldTypeName += "[]";
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


            TypeNode classTypeNode = new TypeNode(className);
            Dictionary<string, TypeNode> propertyTypes = classFields.ToDictionary(f => f.Key, f => new TypeNode(f.Value.FullName));

            var classTypeDefinitionNode = new ClassTypeDefinitionNode(propertyTypes, classTypeNode);

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
            if (!JudgmentKeyword("return")) throw new Exception($"解析返回节点时，当前Token不为关键字“return”。");
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
            var typeToken = _currentToken;
            string typeName = _currentToken.Value; // 类名
            List<ASTNode> ctorArguments = []; // 构造入参
            
            while (true)
            {
                var peekToken = _lexer.PeekToken();
                if (peekToken.Type == TokenType.Dot)
                {
                    NextToken();
                    NextToken();
                    typeName = $"{typeName}.{_currentToken.Value}"; // 向后扩充
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
                else if (peekToken.Type == TokenType.BraceLeft)
                {
                    NextToken(); // 消耗 类型名称
                    break;
                }
            }

            TypeNode typeNode = new TypeNode(typeName);
            typeNode.SetTokenInfo(typeToken);
            ObjectInstantiationNode objectInstantiationNode = new ObjectInstantiationNode(typeNode, ctorArguments);
            List<CtorAssignmentNode> ctorAssignmentNodes = new List<CtorAssignmentNode>(); // 构造器赋值

            /* 类型构造器
                new Type {
                    Value1 = [Exp] ,
                    Value2 = [Exp] ,
                }
            */
            if (_currentToken.Type == TokenType.BraceLeft) 
            {
                NextToken(); // 消耗 "{"
                string propertyName = "";
                Token propertyToken = _currentToken;
                while( _currentToken.Type != TokenType.BraceRight)
                {
                    if (_currentToken.Type == TokenType.Identifier)
                    {
                        propertyToken = _currentToken;
                        propertyName = _currentToken.Value;
                        NextToken();
                        continue;
                    }
                    else if (JudgmentOperator(_currentToken,"="))
                    {
                        NextToken();
                        ASTNode value = ParserExpression();
                        CtorAssignmentNode ctorAssignmentNode = new CtorAssignmentNode(typeNode, propertyName, value);
                        ctorAssignmentNode.SetTokenInfo(propertyToken);
                        ctorAssignmentNodes.Add(ctorAssignmentNode);
                        if(_currentToken .Type == TokenType.Comma)
                        {
                            NextToken(); // 消耗 ","
                            continue;
                        }
                        else if (_currentToken.Type == TokenType.BraceRight)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        throw new Exception($"类型构造器词法分析异常。{_currentToken.ToString()}");
                    }
                }
                NextToken(); // 消耗 "}"
                objectInstantiationNode.SetCtorAssignments(ctorAssignmentNodes);
            }
            
            objectInstantiationNode.SetTokenInfo(instantiationToken);
            return objectInstantiationNode;
        }


        #region 控制流语句解析

        /// <summary>
        /// 解析 if 语句。
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private ASTNode ParseIf()
        {
            NextToken(TokenType.ParenthesisLeft); // 消耗 "if"
            NextToken(); // 消耗 "("
            ASTNode condition = BooleanExpression();
            NextToken(TokenType.BraceLeft); // 消耗 ")"
            NextToken(); // 消耗 "{"
            // 解析大括号中的语句
            List<ASTNode> trueBranch = new List<ASTNode>();
            List<ASTNode> falseBranch = new List<ASTNode>();
            while (_currentToken.Type != TokenType.BraceRight)
            {
                var astNode = Statement(); // 解析 true 分支中的语句
                if (astNode != null)
                {
                    trueBranch.Add(astNode);
                }
            }
            // 确保匹配右大括号 }
            if (_currentToken.Type != TokenType.BraceRight)
            {
                throw new Exception("非预期的标识符 '}' ");
            }
            NextToken();  // 消耗 "}"
            if (_currentToken.Type == TokenType.Keyword && _currentToken.Value == "else")
            {
                NextToken(TokenType.BraceLeft); // 消耗 "else"
                NextToken(); // 消耗 "{"
                while (_currentToken.Type != TokenType.BraceRight)
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
                    throw new Exception("非预期的标识符 '}' ");
                }
                NextToken();  // 消耗 "}"
            }

            return new IfNode(condition, trueBranch, falseBranch).SetTokenInfo(_currentToken);
        }

        /// <summary>
        /// 解析 while 循环语句。
        /// </summary>
        /// <returns></returns>
        private ASTNode ParseWhile()
        {

            NextToken(TokenType.ParenthesisLeft); // 消耗 "while"
            NextToken(); // 消耗 "("
            ASTNode condition = BooleanExpression();
            NextToken(TokenType.BraceLeft); // 消耗 ")"
            NextToken(); // 消耗 "{"
            List<ASTNode> body = new List<ASTNode>();
            while (_currentToken.Type != TokenType.BraceRight)
            {
                var node = Statement();
                if (node is not null)
                {
                    body.Add(node); // 解析循环体中的语句
                }
            }
            _currentToken = _lexer.NextToken(); // 消耗 "}"
            return new WhileNode(condition, body).SetTokenInfo(_currentToken);
        }




        #endregion

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
                    if(_currentToken.Type == TokenType.Semicolon || _currentToken.Type == TokenType.Operator)
                    {
                        return functionCallNode;
                    }
                    targetNode = functionCallNode;
                }
                else if (peekToken2.Type == TokenType.SquareBracketsLeft)
                {
                    // 解析集合获取
                    var collectionIndexNode = ParseCollectionIndexNode(targetNode);
                    if (_currentToken.Type == TokenType.Semicolon || _currentToken.Type == TokenType.Operator)
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
                            TokenType.Comma or TokenType.Operator or TokenType.Dot or TokenType.Semicolon or TokenType.ParenthesisRight 
                                => ParseMemberAccessNode(source), // 获取对象中的成员 source.Value...
                            
                            TokenType.SquareBracketsLeft => ParseCollectionIndexNode(source), // 获取集合中的元素 source[index]....
                            TokenType.ParenthesisLeft => ParseMemberFunctionCallNode(source), // 获取需要调用的方法 source(arg1,arg2...)...
                            _ => throw new Exception($"无法从对象获取成员，当前Token : {peekToken.ToString()}。")
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
                            || peekToken.Type == TokenType.Operator // 可能解析完了方法参数
                            || peekToken.Type == TokenType.BraceRight // 可能解析完了方法参数
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
                var expNode = BooleanExpression();
                if (_currentToken.Type != TokenType.ParenthesisRight)
                    throw new Exception($"解析嵌套表达式时遇到非预期的符号 \"{_currentToken.Type}\"，预期符号为\")\"。");
                NextToken();  // 消耗 ")"
                return expNode;
            }
            else if (_currentToken.Type == TokenType.SquareBracketsLeft)
            {
                NextToken();   // 消耗 "["
                List<ASTNode> elements = [];
                while (_currentToken.Type != TokenType.SquareBracketsRight) // 遇到 "]" 时结束
                {
                    var element = ParserArgNode();
                    elements.Add(element);
                    if (_currentToken.Type == TokenType.Comma)
                    {
                        NextToken(); // 消耗参数分隔符 "," 
                    }
                }
                NextToken();  // 消耗 "]"

                ArrayDefintionNode arrayDefintionNode = new ArrayDefintionNode(elements);
                return arrayDefintionNode.SetTokenInfo(_currentToken);
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
            else if (_currentToken.Type == TokenType.RawString)
            {
                var value = _currentToken.Value;
                NextToken();  // 消耗字符串
                var node = new RawStringNode(value).SetTokenInfo(factorToken);
                return node;
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
                var @char = char.Parse(value);
                return new CharNode(@char).SetTokenInfo(factorToken);
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
                var value = double.Parse(_currentToken.Value);
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

    }



}
