namespace FixtureManagementV3;

public class PageHistoryService
{
    private Stack<String> _history = new Stack<String>();
    public bool CanGoBack {
        get => _history.Count > 0;
    }
    public string BackURL() {
        return _history.Pop();
    }

    public string NavigateBackOr(string url) {
        if (CanGoBack) {
            return BackURL();
        } else {
            return(url);
        }
    }
    public void AddHistory(string url) {
        _history.Push(url);
    }
}
