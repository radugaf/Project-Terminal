class_name Home extends Control

@onready var admin_panel_button: Button = %AdminPanel
@onready var pos_terminal_button: Button = %POSTerminal

func _ready() -> void:
	admin_panel_button.pressed.connect(_on_admin_panel_button_pressed)
	pos_terminal_button.pressed.connect(_on_pos_terminal_button_pressed)

	print(DeviceManager.run_diagnostics())
	print(TerminalManager.RunTerminalDiagnostics())
	print(AuthManager.RunAuthDiagnostics())

	print(AuthManager.IsNewUser)

func _on_admin_panel_button_pressed() -> void:
	print("Farm Price Calculator button pressed")

func _on_pos_terminal_button_pressed() -> void:
	print("POS Terminal button pressed")
