@tool
extends VisualShaderNodeCustom
class_name VisualShaderNodeDeepParallax

func _get_name():
	return "DeepParallax"

func _get_category():
	return "3D"

func _get_subcategory():
	return "Depth"

func _get_description():
	return "Deep Parallax"

func _get_return_icon_type():
	return VisualShaderNode.PORT_TYPE_SCALAR

func _get_input_port_count():
	return 6

func _get_input_port_name(port):
	match port:
		0:
			return "uv"
		1:
			return "texture"
		2:
			return "heightmap scale"
		3:
			return "min layers"
		4:
			return "max layers"
		5:
			return "depth flip"


func _get_input_port_type(port):
	match port:
		0:
			return VisualShaderNode.PORT_TYPE_VECTOR_2D
		1:
			return VisualShaderNode.PORT_TYPE_SAMPLER
		2:
			return VisualShaderNode.PORT_TYPE_SCALAR
		3:
			return VisualShaderNode.PORT_TYPE_SCALAR_INT
		4:
			return VisualShaderNode.PORT_TYPE_SCALAR_INT
		5:
			return VisualShaderNode.PORT_TYPE_VECTOR_2D


func _get_output_port_count():
	return 1

func _get_output_port_name(port):
	match port:
		0:
			return "uv"

func _get_output_port_type(port):
	match port:
		0:
			return VisualShaderNode.PORT_TYPE_VECTOR_2D

func _get_global_code(mode):
	if mode != Shader.MODE_SPATIAL:
		return ""
		
	var code = preload("deep_parallax.gdshader").code
	code = code.replace("shader_type spatial;\n", "")
	return code

func _get_code(input_vars, output_vars, mode, type):
	if mode != Shader.MODE_SPATIAL or type != VisualShader.TYPE_FRAGMENT:
		return ""
	
	# Default values
	var uv = "UV"
	var texture = "texture_parallax_default"
#	var depth_scale = "0.05"
	var heightmap_scale = "heightmap_scale"
	var min_layers = "8"
	var max_layers = "32"
	var vertex = "VERTEX"
	var normal = "NORMAL"
	var tangent = "TANGENT"
	var binormal = "BINORMAL"
	var eyeoffset = "EYE_OFFSET"
	var depth_flip = "_depth_flip"

	if input_vars[0]:
		uv = input_vars[0]
	if input_vars[1]:
		texture = input_vars[1]
	if input_vars[2]:
		heightmap_scale = input_vars[2]	
	if input_vars[3]:
		min_layers = input_vars[3]
	if input_vars[4]:
		max_layers = input_vars[4]	
	if input_vars[5]:
		depth_flip = input_vars[5]


	var params =  [uv, texture, heightmap_scale, min_layers, max_layers, vertex, normal, tangent, binormal, eyeoffset, output_vars[0]]
	return "deep_parallax(%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s);" % params

func _init(): 
	# Default values for the editor
	# depth_scalefloatcon
#	if not get_input_port_default_value(2):
#		set_input_port_default_value(2, 0.05)
	# min_layers
	if not get_input_port_default_value(3):
		set_input_port_default_value(3, 8)
	# max_layers
	if not get_input_port_default_value(4):
		set_input_port_default_value(4, 32)
	# depth_flip
	if not get_input_port_default_value(5):
		set_input_port_default_value(5, Vector2(1.0, 1.0))
