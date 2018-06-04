using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MVCForumAutomation.Infrastructure;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.Events;
using OpenQA.Selenium.Support.Extensions;
using TestAutomationEssentials.Common;

namespace MVCForumAutomation.PageObjects
{
    public class MVCForumClient : ITakesScreenshot
    {
        private readonly TestDefaults _testDefaults;
        private readonly IWebDriver _webDriver;
        private readonly Lazy<string> _adminPassword;

        public MVCForumClient(TestDefaults testDefaults, TestEnvironment environment)
        {
            _adminPassword = new Lazy<string>(GetAdminPassword);

            _testDefaults = testDefaults;
            // TODO: select the type of browser and the URL from a configuration file
            var parentDriver = CreateDriver(environment);
            var eventFiringDriver = new EventFiringWebDriver(parentDriver);
            VisualLogger.RegisterWebDriverEvents(eventFiringDriver);
            _webDriver = eventFiringDriver;
            _webDriver.Url = environment.URL;
        }

        private static IWebDriver CreateDriver(TestEnvironment environment)
        {
            switch (environment.BrowserType)
            {
                case TestEnvironment.BrowserTypes.Edge:
                    return new EdgeDriver();

                case TestEnvironment.BrowserTypes.Firefox:
                    return new FirefoxDriver();

                case TestEnvironment.BrowserTypes.Chrome:
                    return new ChromeDriver();

                default:
                    throw new NotSupportedException($"Browser {environment.BrowserType} is not supported");
            }
        }

        ~MVCForumClient()
        {
            _webDriver.Quit();
        }

        public LoggedInUser RegisterNewUserAndLogin()
        {
            var username = UniqueIdentifier.For("User");
            const string password = "123456";
            var email = $"abc@{UniqueIdentifier.For("domain")}.com";

            var registrationPage = GoToRegistrationPage();
            registrationPage.Username = username;
            registrationPage.Password = password;
            registrationPage.ConfirmPassword = password;
            registrationPage.Email = email;

            registrationPage.Register();

            return new LoggedInUser(_webDriver, _testDefaults);
        }

        private RegistrationPage GoToRegistrationPage()
        {
            var registerLink = _webDriver.FindElement(By.ClassName("auto-register"));
            registerLink.Click();

            return new RegistrationPage(_webDriver);
        }

        private LoginPage GoToLoginPage()
        {
            var logonLink = _webDriver.FindElement(By.ClassName("auto-logon"));
            logonLink.Click();

            return new LoginPage(_webDriver);
        }

        public LatestDiscussions LatestDiscussions
        {
            get { return new LatestDiscussions(_webDriver); }
        }

        public AdminConsole OpenAdminConsole()
        {
            var adminUser = LoginAsAdmin(_adminPassword.Value);
            var adminConsole = adminUser.GoToAdminConsole();

            return adminConsole;
        }

        public CategoriesList Categories
        {
            get
            {
                return new CategoriesList(_webDriver);
            }
        }

        public LoggedInAdmin LoginAsAdmin(string adminPassword)
        {
            return LoginAs(_testDefaults.AdminUsername, adminPassword, () => new LoggedInAdmin(_webDriver, _testDefaults));
        }

        private TLoggedInUser LoginAs<TLoggedInUser>(string username, string password, Func<TLoggedInUser> createLoggedInUser)
            where TLoggedInUser : LoggedInUser
        {
            using (Logger.StartSection($"Logging in as '{username}'/'{password}'"))
            {
                var loginPage = GoToLoginPage();
                loginPage.Username = username;
                loginPage.Password = password;
                loginPage.LogOn();

                var loginErrorMessage = loginPage.GetErrorMessageIfExists();
                Assert.IsNull(loginErrorMessage,
                    $"Login failed for user:{username} and password:{password}. Error message: {loginErrorMessage}");

                return createLoggedInUser();
            }
        }

        public void TakeScreenshot()
        {
            var screenshot = GetScreenshotInternal();
            VisualLogger.AddScreenshot(screenshot);
        }

        Screenshot ITakesScreenshot.GetScreenshot()
        {
            return GetScreenshotInternal();
        }

        private Screenshot GetScreenshotInternal()
        {
            return _webDriver.TakeScreenshot();
        }

        private string GetAdminPassword()
        {
            using (Logger.StartSection("Getting Admin password from 'Read Me' topic"))
            {
                var readMeHeader = LatestDiscussions.Bottom;
                var readmeTopic = readMeHeader.OpenDiscussion();
                var body = readmeTopic.BodyElement;
                var password = body.FindElement(By.XPath(".//strong[2]"));
                var adminPassword = password.Text;
                Logger.WriteLine($"Admin password='{adminPassword}'");
                return adminPassword;
            }
        }
    }
}