using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.IE;
using Serein.NodeFlow;

namespace Serein.Module
{
    public enum DriverType
    {
        Chrome,
        Edge,
        IE,
        Firefox,
    }
    public enum ByType
    {
        XPath,
        Id,
        Class,
        Name,
        CssSelector,
        PartialLinkText,
    }
    public enum ScriptOp
    {
        Add,
        Modify,
        Delete,
    }
    public enum ActionType
    {
        Click,
        DoubleClick,
        RightClick,
        SendKeys
    }


    /// <summary>
    /// 网页自动化测试
    /// Web test automation
    /// </summary>
    [DynamicFlow]
    public class WebSelenium
    {

        public WebDriver WebDriver { get; set; }

        
        public DriverType DriverType { get; set; }

        //public ChromeDriver Driver { get; set; }
        #region Init and Exit
        [MethodDetail(DynamicNodeType.Init)]
        public void Init(DynamicContext context)
        {
        }

        [MethodDetail(DynamicNodeType.Exit)]
        public void Exit(DynamicContext context)
        {
            WebDriver?.Quit();
        }
        #endregion

        [MethodDetail(DynamicNodeType.Action,"等待")]
        public void Wait([Explicit]int time = 1000)
        {
            Thread.Sleep(time);
            
        }

        [MethodDetail(DynamicNodeType.Action,"启动浏览器")]
        public WebDriver OpenDriver([Explicit] bool isVisible = true,[Explicit] DriverType driverType = DriverType.Chrome)
        {


            if(driverType == DriverType.Chrome)
            {
                ChromeOptions options = new ChromeOptions();
                if (!isVisible)
                {
                    options.AddArgument("headless"); // 添加无头模式参数
                    options.AddArgument("disable-gpu"); // 需要禁用 GPU
                    options.LeaveBrowserRunning = true;  // 设置浏览器不自动关闭
                }
                WebDriver = new ChromeDriver(options);
                return WebDriver;
            }
            else if (driverType == DriverType.Edge)
            {
                EdgeOptions options = new EdgeOptions();
                if (!isVisible)
                {
                    options.AddArgument("headless"); // 添加无头模式参数
                    options.AddArgument("disable-gpu"); // 需要禁用 GPU
                    options.LeaveBrowserRunning = true;  // 设置浏览器不自动关闭
                }
                WebDriver = new EdgeDriver(options);
                return WebDriver;
            }
            else if (driverType == DriverType.IE)
            {
                InternetExplorerOptions options = new InternetExplorerOptions();
                // IE浏览器不支持无头模式，因此这里不添加无头模式参数
                WebDriver = new InternetExplorerDriver(options);
                return WebDriver;
            }
            else if (driverType == DriverType.Firefox)
            {
                FirefoxOptions options = new FirefoxOptions();
                if (!isVisible)
                {
                    options.AddArgument("-headless"); // 添加无头模式参数
                                                      // Firefox没有直接的LeaveBrowserRunning选项，但可以使用调试器暂停关闭
                }

                // FirefoxDriver 没有直接的 LeaveBrowserRunning 选项
                WebDriver = new FirefoxDriver(options);
                return WebDriver;
            }
            else
            {
                throw new InvalidOperationException("");
            }
            
        }

        [MethodDetail(DynamicNodeType.Action,"进入网页")]
        public void ToPage([Explicit] string url)
        {
            if (url.StartsWith("https://") || url.StartsWith("http://"))
            {
                WebDriver.Navigate().GoToUrl($"{url}");
            }
            else
            {
                throw new Exception("请输入完整的Url。Please enter the full Url.");
            }
        }


        [MethodDetail(DynamicNodeType.Action,"定位元素")]
        public IWebElement FindElement([Explicit] string key = "", [Explicit] ByType byType = ByType.XPath, [Explicit] int index = 0)
        {
            By by = byType switch
            {
                ByType.Id => By.Id(key),
                ByType.XPath => By.XPath(key),
                ByType.Class => By.ClassName(key),
                ByType.Name => By.Name(key),
                ByType.CssSelector => By.CssSelector(key),
                ByType.PartialLinkText => By.PartialLinkText(key),
            };
            var element = WebDriver.FindElements(by)[index];
            return element;
        }
        [MethodDetail(DynamicNodeType.Action, "操作元素")]
        public void PerformAction(IWebElement element, [Explicit] ActionType actionType = ActionType.Click, [Explicit] string text = "")
        {
            var actions = new OpenQA.Selenium.Interactions.Actions(WebDriver);

            switch (actionType)
            {
                case ActionType.Click:
                    actions.Click(element).Perform();
                    break;
                case ActionType.DoubleClick:
                    actions.DoubleClick(element).Perform();
                    break;
                case ActionType.RightClick:
                    actions.ContextClick(element).Perform();
                    break;
                case ActionType.SendKeys:
                    element.Click();
                    element.Clear();
                    element.SendKeys(text);
                    break;
            }
        }


        [MethodDetail(DynamicNodeType.Action,"Js操作元素属性")]
        public void AddAttribute(IWebElement element, [Explicit] ScriptOp scriptOp = ScriptOp.Modify,[Explicit] string attributeName = "", [Explicit] string value = "")
        {
            if(scriptOp == ScriptOp.Add)
            {
                WebDriver.ExecuteScript($"arguments[0].{attributeName} = arguments[1];", element, value);
            }
            else if (scriptOp == ScriptOp.Modify)
            {
                WebDriver.ExecuteScript("arguments[0].setAttribute(arguments[1], arguments[2]);", element, attributeName, value);
            }
            else if (scriptOp == ScriptOp.Delete)
            {
                WebDriver.ExecuteScript("arguments[0].removeAttribute(arguments[1]);", element, attributeName);
            }
        }


        [MethodDetail(DynamicNodeType.Action, "Js获取元素属性")]
        public string GetAttribute(IWebElement element, [Explicit] string attributeName = "")
        {
            return element.GetAttribute(attributeName);
        }
    }
}
