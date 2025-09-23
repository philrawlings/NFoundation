namespace NFoundation.Photino.NET.Extensions
{
    public interface IWindowManager
    {
        Window GetWindow<T>() where T : Window;
    }
}
