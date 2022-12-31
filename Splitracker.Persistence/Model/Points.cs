namespace Splitracker.Persistence.Model;

record Points(int Channeled, int Exhausted, int Consumed)
{
    public int Channeled { get; set; } = Channeled;
    public int Exhausted { get; set; } = Exhausted;
    public int Consumed { get; set; } = Consumed;

    public void Deconstruct(out int channeled, out int exhausted, out int consumed)
    {
        channeled = Channeled;
        exhausted = Exhausted;
        consumed = Consumed;
    }
}