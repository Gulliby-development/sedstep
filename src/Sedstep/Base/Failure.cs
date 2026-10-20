namespace Sedstep;

public sealed class Failure : Exception
{
    public Failure(string message) : base(message)
    {
    }
}

