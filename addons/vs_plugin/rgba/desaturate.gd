@tool
class_name VisualShaderNodeDesaturate
extends VisualShaderNodeCustom


func _get_name() -> String:
	return "Desaturate"


func _get_category() -> String:
	return "rgba"


func _get_description() -> String:
	return "Desaturate"


func _get_return_icon_type() -> PortType:
	return PORT_TYPE_VECTOR_4D


func _get_input_port_count() -> int:
	return 2


func _get_input_port_name(port):
	match port:
		0:
			return "color";
		1:
			return "amount"

func _get_input_port_type(port):
	match port:
		0:
			return PORT_TYPE_VECTOR_4D;
		1:
			return PORT_TYPE_SCALAR;


func _get_output_port_count() -> int:
	return 1


func _get_output_port_name(port) -> String:
	return "result"


func _get_output_port_type(port) -> PortType:
	return PORT_TYPE_VECTOR_4D

func _get_global_code(mode) -> String:
	var code = preload("desaturate.gdshader").code
	code = code.replace("shader_type spatial;\n", "")
	return code
	
func _get_code(input_vars: Array[String], output_vars: Array[String], mode: Shader.Mode, type: VisualShader.Type) -> String:
	# Default values
	var color = "vec3(1.0, 1.0, 1.0)"
	var sat_amount = "0.0"
	
	if input_vars[0]:
		color = input_vars[0]
	if input_vars[1]:
		sat_amount = input_vars[1]
		
	var params =  [color, sat_amount, output_vars[0]]
	return "dEsaTuraTi0n(%s, %s, %s);" % params
	
#func _init():
	# Default values for Editor
#	if not get_input_port_default_value(1):
#		set_input_port_default_value(1, 1.7)
