namespace HelloApp.Tests;

public class BasicTests
{
    [Before(Class)]
    public static Task BeforeClass(ClassHookContext context)
    {
        // Runs once before all tests in this class
        return Task.CompletedTask;
    }

    [After(Class)]
    public static Task AfterClass(ClassHookContext context)
    {
        // Runs once after all tests in this class
        return Task.CompletedTask;
    }

    [Before(Test)]
    public Task BeforeTest(TestContext context)
    {
        // Runs before each test in this class
        return Task.CompletedTask;
    }

    [After(Test)]
    public Task AfterTest(TestContext context)
    {
        // Runs after each test in this class
        return Task.CompletedTask;
    }

    [Test]
    public async Task LessThanTest()
    {
        var result = 1;
        await Assert.That(result).IsLessThan(2);
    }
}
