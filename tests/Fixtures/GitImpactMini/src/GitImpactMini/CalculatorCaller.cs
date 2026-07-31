namespace GitImpactMini;

public class CalculatorCaller
{
    public int Run()
    {
        var calculator = new Calculator();
        return calculator.Add(1, 2);
    }
}
