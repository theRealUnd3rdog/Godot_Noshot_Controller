# Source: https://github.com/arkology/ShaderV

@tool
class_name VisualShaderNodeUVrotate
extends VisualShaderNodeCustom

func _init() -> void:
	set_input_port_default_value(1, 0.0)
	set_input_port_default_value(2, Vector2(0.5, 0.5))

func _get_name() -> String:
	return "RotateUV"

func _get_category() -> String:
	return "UV"

#func _get_subcategory():
#	return ""

func _get_description() -> String:
	return "Rotate UV by angle relative to pivot vector"

func _get_return_icon_type():
	return VisualShaderNode.PORT_TYPE_VECTOR_2D

func _get_input_port_count() -> int:
	return 3

func _get_input_port_name(port: int):
	match port:
		0:
			return "uv"
		1:
			return "angle (rad.)"
		2:
			return "pivot"

func _get_input_port_type(port: int):
	match port:
		0:
			return VisualShaderNode.PORT_TYPE_VECTOR_2D
		1:
			return VisualShaderNode.PORT_TYPE_SCALAR
		2:
			return VisualShaderNode.PORT_TYPE_VECTOR_2D

func _get_output_port_count() -> int:
	return 1

func _get_output_port_name(port: int) -> String:
	return "uv"

func _get_output_port_type(port: int):
	return VisualShaderNode.PORT_TYPE_VECTOR_2D

func _get_global_code(mode : Shader.Mode) -> String:
	var code = preload("rotate.gdshader").code
	code = code.replace("shader_type spatial;\n", "")
	return code

func _get_code(input_vars: Array[String], output_vars: Array[String], mode: Shader.Mode, type: VisualShader.Type):
	var uv = "UV"
	
	if input_vars[0]:
		uv = input_vars[0]
	
	return output_vars[0] + " = r0tateUVFunc(%s.xy, %s.xy, %s);" % [uv, input_vars[2], input_vars[1]]
