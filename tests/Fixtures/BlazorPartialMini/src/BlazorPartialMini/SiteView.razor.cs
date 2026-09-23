using Microsoft.AspNetCore.Components;
using System.Threading.Tasks;

namespace BlazorPartialMini;

public partial class SiteView
{
    private string Title => "Ready";

    private void HandleClick() { }

    private Task HandleConfirm() => Task.CompletedTask;

    private void UnusedControlMember() { }

    protected override Task OnInitializedAsync() => base.OnInitializedAsync();

    protected override void OnParametersSet() => base.OnParametersSet();
}
