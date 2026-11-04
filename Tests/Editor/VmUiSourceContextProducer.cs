namespace VMUnityAutomation.Editor.Tests
{
    public static class VmUiSourceContextProducer
    {
        public static void Publish(VmUiSourceContextValue value) => VmUiSourceContextConsumer.Consume(value);
    }
}
