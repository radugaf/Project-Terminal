class_name Home extends Control


@onready var farm_price_calc_nav_button: Button = %FarmPrintPriceCalculatorNavButton

func _ready() -> void:
    # Connect the pressed signal to our custom function
    farm_price_calc_nav_button.pressed.connect(_on_farm_price_calc_nav_button_pressed)

func _on_farm_price_calc_nav_button_pressed() -> void:
    print("Farm Price Calculator button pressed")
