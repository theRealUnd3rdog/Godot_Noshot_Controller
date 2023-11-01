@tool
class_name VisualShaderNodeLerpFrame
extends VisualShaderNodeCustom


func _get_name() -> String:
	return "LerpFrame"


func _get_category() -> String:
	return "Utility"


func _get_description() -> String:
	return "LerpFrame using flow map"


func _get_return_icon_type() -> PortType:
	return PORT_TYPE_VECTOR_4D


func _get_input_port_count() -> int:
	return 3


func _get_input_port_name(port):
	match port:
		0:
			return "lerpframe data"
		1:
			return "albedo"
		2:
			return "flow map"


func _get_input_port_type(port):
	match port:
		0:
			return PORT_TYPE_VECTOR_3D
		1:
			return PORT_TYPE_SAMPLER
		2:
			return PORT_TYPE_SAMPLER


func _get_output_port_count() -> int:
	return 1

func _get_output_port_name(port):
	match port:
		0:
			return "albedo"


func _get_output_port_type(port):
	match port:
		0:
			return PORT_TYPE_VECTOR_4D


func _get_global_code(mode) -> String:
	var code = preload("lerpframe.gdshader").code
	code = code.replace("shader_type spatial;\n", "")
	return code
	

func _get_code(input_vars: Array[String], output_vars: Array[String], mode: Shader.Mode, type: VisualShader.Type) -> String:
	# Default values
	var uv: String = ""
	# var rate: String = "1.0"
	
	# if input_vars[0]:
	# 	uv = input_vars[0]
	# if input_vars[1]:
	# 	rate = input_vars[1]
		
	var params = [input_vars[0], input_vars[1], input_vars[2], output_vars[0]]
	return "lerPframe(UV, COLOR, %s, %s, %s, %s);" % params
	

# func _init():
# 	# Default values for Editor
# 	if not get_input_port_default_value(1):
# 		set_input_port_default_value(1, 1.0)
