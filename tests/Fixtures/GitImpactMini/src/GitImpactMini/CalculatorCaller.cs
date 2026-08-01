namespace GitImpactMini;

public class CalculatorCaller
{
    public int Run()
    {
        var calculator = new Calculator();
        return calculator.Add(1, 2);
    }

    public int RunTwice()
    {
        var calculator = new Calculator();
        return calculator.Add(1, 2) + calculator.Add(3, 4);
    }

    public int RunThrice()
    {
        var calculator = new Calculator();
        return calculator.Add(1, 2) + calculator.Add(3, 4) + calculator.Add(5, 6);
    }
}
