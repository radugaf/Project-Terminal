class_name FarmPriceCalc extends Control


@onready var print_time_input: LineEdit = %PrintTime # hh:mm:ss
@onready var labor_time_input: LineEdit = %LaborTime # hh:mm:ss
@onready var filament_cost_input: LineEdit = %FilamentCost # $/Kg
@onready var weight_input: LineEdit = %Weight # g

@onready var filament_cost_result: Label = %FilamentCostResult
@onready var labor_cost_result: Label = %LaborCostResult
@onready var utilities_cost_result: Label = %UtilitiesCostResult
@onready var machine_cost_result: Label = %MachineCostResult
@onready var maintenance_cost_result: Label = %MaintenanceCostResult
@onready var overhead_multiplier_result: Label = %OverheadMultiplierResult
@onready var total_cost_result: Label = %TotalCostResult

@export var printer_power_wattage: int = 500
@export var electricity_cost_per_kwh: float = 1.5
@export var machine_cost_per_hour: float = 0.5
@export var maintenance_cost_per_hour: float = 0.25
@export var overhead_multiplier: float = 1.2
@export var labor_cost_per_hour: float = 100.0


func _ready() -> void:
	print_time_input.connect("text_changed", _calculate_costs)
	labor_time_input.connect("text_changed", _calculate_costs)
	filament_cost_input.connect("text_changed", _calculate_costs)
	weight_input.connect("text_changed", _calculate_costs)


func _calculate_costs(_new_text: String) -> void:
	# 1) Parse the user inputs safely. If conversion fails, fallback to 0.
	var print_time_hours = parse_time_to_hours(print_time_input.text)
	var labor_time_hours = parse_time_to_hours(labor_time_input.text)
	var filament_cost_kg = filament_cost_input.text.to_float()
	var weight_in_grams = weight_input.text.to_float()

	# 2) Compute each cost component

	# Filament cost:
	var filament_cost = filament_cost_kg * (weight_in_grams / 1000.0)

	# Labor cost:
	var labor_cost = labor_time_hours * labor_cost_per_hour

	# Utilities (electricity) cost:
	var utilities_cost = (printer_power_wattage / 1000.0) * print_time_hours * electricity_cost_per_kwh

	# Machine cost:
	var machine_cost = machine_cost_per_hour * print_time_hours

	# Maintenance cost:
	var maintenance_cost = maintenance_cost_per_hour * print_time_hours

	# Sum the direct costs before overhead
	var direct_total = filament_cost + labor_cost + utilities_cost + machine_cost + maintenance_cost

	# 3) Multiply by overhead factor
	var overhead_total = direct_total * overhead_multiplier

	# 4) Update the UI labels
	filament_cost_result.text = "RON " + str(filament_cost)
	labor_cost_result.text = "RON " + str(labor_cost)
	utilities_cost_result.text = "RON " + str(utilities_cost)
	machine_cost_result.text = "RON " + str(machine_cost)
	maintenance_cost_result.text = "RON " + str(maintenance_cost)
	overhead_multiplier_result.text = "x" + str(overhead_multiplier)
	total_cost_result.text = "RON " + str(overhead_total)


# Helper function to parse "hh:mm:ss" into hours as a float.
# Returns 0.0 if the string does not match the expected format.
#
func parse_time_to_hours(time_str: String) -> float:
	var parts = time_str.split(":")
	if parts.size() != 3:
		return 0.0

	var hours = parts[0].to_int()
	var minutes = parts[1].to_int()
	var seconds = parts[2].to_int()

	return float(hours) + float(minutes) / 60.0 + float(seconds) / 3600.0