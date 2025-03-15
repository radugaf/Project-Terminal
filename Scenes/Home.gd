class_name Home extends Control


@onready var admin_panel_button: Button = %AdminPanel
@onready var pos_terminal_button: Button = %POSTerminal

func _ready() -> void:
	admin_panel_button.pressed.connect(_on_admin_panel_button_pressed)
	pos_terminal_button.pressed.connect(_on_pos_terminal_button_pressed)

func _on_admin_panel_button_pressed() -> void:
	print("Farm Price Calculator button pressed")

func _on_pos_terminal_button_pressed() -> void:
	print("POS Terminal button pressed")


# First check if the organization has any staff other than the Staff Owners registered
# If not then we show the POSTerminal button disabled
# Check if the organization has this terminal enabled. The logic is that
# this way we can keep track on which devices are enabled and which are not
# If the terminal is not registered then we start the register the terminal
