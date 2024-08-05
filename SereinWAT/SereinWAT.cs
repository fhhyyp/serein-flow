using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.IE;
using Serein.DynamicFlow;
using Serein.DynamicFlow.Tool;
using Serein.Web;
using System.Diagnostics.CodeAnalysis;

namespace Serein.Module
{
    public enum DriverType
    {
        Chrome,
        Edge,
        IE,
        Firefox,
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

        [MethodDetail(DynamicNodeType.Action,"启动浏览器")]
        public WebDriver OpenDriver([Explicit] bool isVisible = true,
                                    [Explicit] DriverType driverType = DriverType.Chrome)
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

        [MethodDetail(DynamicNodeType.Action,"等待")]
        public void Wait([Explicit]int time = 1000)
        {
            Thread.Sleep(time);
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

        [MethodDetail(DynamicNodeType.Action,"XPath定位元素")]
        public IWebElement XPathFind(string xpath,
                              [Explicit] int index = 0)
        {
            var element = WebDriver.FindElements(By.XPath(xpath))[0];
            return element;
        }

        [MethodDetail(DynamicNodeType.Action,"Js添加元素内部属性")]
        public void AddAttribute(IWebElement element, [Explicit] string attributeName = "", [Explicit] string value = "")
        {
            //IJavaScriptExecutor js = (IJavaScriptExecutor)Driver;
            WebDriver.ExecuteScript($"arguments[0].{attributeName} = arguments[1];", element, value);
        }

        [MethodDetail(DynamicNodeType.Action, "Js设置元素内部属性")]
        public void SetAttribute(IWebElement element, [Explicit] string attributeName = "", [Explicit] string value = "")
        {
            //IJavaScriptExecutor js = (IJavaScriptExecutor)Driver;
            WebDriver.ExecuteScript("arguments[0].setAttribute(arguments[1], arguments[2]);", element, attributeName, value);
        }

        [MethodDetail(DynamicNodeType.Action, "Js获取元素内部属性")]
        public string GetAttribute(IWebElement element, [Explicit] string attributeName = "")
        {
            return element.GetAttribute(attributeName);
        }
    }
}
