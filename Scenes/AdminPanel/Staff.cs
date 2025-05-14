using Godot;
using ProjectTerminal.Resources.Admin;
using System;

public partial class StaffView : BaseContentController
{

    private VBoxContainer _staffTable;
    public override void _Ready()
    {
        _staffTable = GetNode<VBoxContainer>("%StaffTable");
    }
}
