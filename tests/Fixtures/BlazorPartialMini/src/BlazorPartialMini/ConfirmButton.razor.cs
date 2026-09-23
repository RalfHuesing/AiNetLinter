#nullable enable

using Microsoft.AspNetCore.Components;

namespace BlazorPartialMini;

public partial class ConfirmButton
{
    [Parameter]
    public EventCallback OnConfirm { get; set; }
}
