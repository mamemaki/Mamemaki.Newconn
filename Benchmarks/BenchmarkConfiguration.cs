namespace Benchmarks;

internal class BenchmarkConfiguration
{
    public int Clients { get; set; } = 6;

    public byte[] Message { get; set; } = "datadatadatadatadatadatadatadatadatadatadatadatadata\r\n"u8.ToArray();

    public int MessageCount { get; set; } = 1000;

    public string Address { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 1234;
}
