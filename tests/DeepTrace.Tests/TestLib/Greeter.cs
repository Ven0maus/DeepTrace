namespace TestLib;

public class Greeter
{
    private readonly string _prefix;

    public Greeter(string prefix = "Hello")
    {
        _prefix = prefix;
    }

    public string Greet(string name)
    {
        return $"{_prefix}, {name}!";
    }

    public static void Main()
    {
        var greeter = new Greeter();
        Console.WriteLine(greeter.Greet("World"));
    }
}
