using Famick.HomeManagement.Mobile.Pages.Contacts;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Controls;

public partial class GroupMemberSectionHeader : ContentView
{
    public static readonly BindableProperty GroupIdProperty =
        BindableProperty.Create(nameof(GroupId), typeof(Guid), typeof(GroupMemberSectionHeader), Guid.Empty);

    public Guid GroupId
    {
        get => (Guid)GetValue(GroupIdProperty);
        set => SetValue(GroupIdProperty, value);
    }

    public event EventHandler? MemberAdded;

    public GroupMemberSectionHeader()
    {
        InitializeComponent();
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        if (GroupId == Guid.Empty) return;
        await Shell.Current.GoToAsync(nameof(ContactEditPage), new Dictionary<string, object>
        {
            { "ContactId", string.Empty },
            { "ParentGroupId", GroupId.ToString() }
        });
        MemberAdded?.Invoke(this, EventArgs.Empty);
    }
}
