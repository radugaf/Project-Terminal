extends Node

var env = {}

func _ready():
    var file = FileAccess.open("res://.env", FileAccess.READ)
    if file:
        while !file.eof_reached():
            var line = file.get_line().strip_edges()
            if line.length() > 0 and !line.begins_with("#"):
                var key_value = line.split("=")
                if key_value.size() == 2:
                    env[key_value[0]] = key_value[1]
        file.close()
    else:
        print("Failed to open .env file")

func get_env(key: String) -> String:
    return env[key]