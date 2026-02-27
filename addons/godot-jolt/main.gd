@icon("gj_icon.png")
class_name GameJoltAPI extends HTTPRequest

# | Credits |
	# Original Godot GJ API created by Ackens
	# Forked from Deakcor's Godot GJ API
	# > https://github.com/deakcor/-godot-gj-api
	#
	# Ported to Godot 4.x by IrønBrandon
	# > https://github.com/IronBrandon/Godot-GameJolt-API/

# NOTE: Please view the cleaner documentation by pressing F1 and typing 'GameJoltAPI'
# or right-click "GameJoltAPI" in the node's inspector and select "Open Documentation".
# If you don't see the GameJoltAPI as its own class, save and reload your project.

## A simple GameJolt API node.
## 
## [b]Credits[/b]: Ackens, Piotr Rojewski, Deakcor, and IrønBrandon.[br][br]
## 
## Also look at the [GameJoltAPI.Request] class.[br][br]
## 
## Any methods without a description are untested, if you find any issues please
## report them in the GitHub repository.[br][br]
## 
## [url=https://github.com/IronBrandon/Godot-GameJolt-API/wiki]GitHub Wiki[/url][br]
## Look at the [url=https://gamejolt.com/game-api/doc]Official Game API Docs[/url] for more information
## 
## @tutorial(Authenticating Users): https://github.com/IronBrandon/Godot-GameJolt-API#authenticating-users
## @tutorial(Unlocking Trophies): https://github.com/IronBrandon/Godot-GameJolt-API#unlocking-trophies
## @tutorial(Fetching Any Data): https://github.com/IronBrandon/Godot-GameJolt-API#fetching-any-data

## Emits after a request to GameJolt has completed.
## [br][br][param message] will follow the format [code]{"success": true/false, . . .}[/code]
## [br][param type] will be one of the request types, such as "/users/auth/", "/users/", "/trophies/", "/sessions/check/", "/sessions/open/", etc.
signal gamejolt_request_completed(type: String, message: Dictionary)

const BASE_GAMEJOLT_API_URL = 'https://api.gamejolt.com/api/game/v1_2'

@export_category("Right-Click 'GameJoltAPI' to see documentation")
@export var private_key: String ## Your game's private key.
@export var game_id: String ## Your game's ID. This is public, just look at your game's URL and copy the numbers.
## Optional path to a JSON file containing your game's private_key and game_id. Format:
## [codeblock]{"private_key":"<gamekey>","game_id":"<gameid>"}[/codeblock] Useful for open-source games, just add the file to your '.gitignore'.[br][br]
## If you want to make it extra secure, you can also add a [param "trophies"] key with either an Array of trophy IDs.[br]
## Look at [member detected_trophies] for more info.
@export_file("*.json") var data_path: String
@export var auto_batch: bool = true ## Merge queued requests in one batch request.
@export var auto_auth_in_ready: bool = false ## Automatically calls the [method user_auto_auth] method during ready.
@export var verbose: bool = false ## Outputs detailed text. Should turn this off in release builds.
@export var raw_batch_requests: bool = false ## Emits raw "/batch/" requests through [signal gamejolt_request_completed].

var _username_cache: String
var _user_token_cache: String
var _busy: bool
var trophy_ids: PackedInt32Array ## Contains trophies loaded from [member data_path].
var queue: Array[Request] = [] ## The current queue of [param Request]s.
var current_request: Request ## The currently active GameJolt request.

## The class that handles all data for API calls.
## 
## To create a new [GameJoltAPI.Request] type [codeblock]GameJoltAPI.Request.new("/type/", {}, ["/sub/","/types/"])[/codeblock]
class Request:
	var type: String ## The call type, such as "/users/", "/users/auth/", "/scores/", etc.
	var parameters: Dictionary ## The parameters used for the call.
	var sub_types: Array = [] ## Sub types. Only used for batch requests.
	
	func _init(new_type: String, new_parameters: Dictionary, new_sub_types: Array = []):
		type = new_type; parameters = new_parameters; sub_types = new_sub_types

func _init() -> void:
	request_completed.connect(_on_HTTPRequest_request_completed)

func _ready() -> void:
	if !data_path.is_empty():
		var file = ResourceLoader.load(data_path,"",ResourceLoader.CACHE_MODE_IGNORE)
		if file is JSON:
			var data = file.data
			if data is Dictionary:
				private_key = data.get("private_key", private_key)
				game_id = data.get("game_id", game_id)
				if data.has("trophies"): for trophy in data["trophies"]:
					trophy_ids.append(trophy)
		else: push_warning('[GAMEJOLT] Key Path should lead to a .json file with "private_key" and "game_id" as keys')
	if auto_auth_in_ready:
		user_auto_auth()

func _call_gj_api(type:String, parameters:Dictionary={}, sub_types:Array=[], include_user:bool=true) -> Error:
	if include_user:
		parameters["username"] = parameters.get("username", _username_cache)
		parameters["user_token"] = parameters.get("user_token", _user_token_cache)
	if _busy:
		if auto_batch and type != '/batch/':
			var url: String = _compose_url(type, parameters, true)
			if queue.is_empty() or queue.back().type != '/batch/' or queue.back().sub_types.size()>=50:
				queue.push_back(Request.new('/batch/',{requests = [url]},[type]))
			else:
				queue.back().parameters.requests.push_back(url)
				queue.back().sub_types.push_back(type)
		else:
			queue.push_back(Request.new(type,parameters,sub_types))
		return ERR_BUSY
	_busy = true
	var url: String = _compose_url(type, parameters)
	_verbose(type+": Composed URL (" + url + ")")
	current_request = Request.new(type,parameters,sub_types)
	var err = request(url)
	if err != OK:
		_busy = false
		return err
	return OK

func _compose_param(parameter, key:String) -> String:
	parameter = str(parameter)
	if parameter.is_empty(): # If the key is an empty string, don't include it at all.
		return ""
	return '&'+key+'='+parameter.uri_encode()

func _compose_url(url_path:String, parameters:Dictionary={}, sub_request:bool=false) -> String:
	var final_url:String = ("" if sub_request else BASE_GAMEJOLT_API_URL) + url_path
	final_url += '?game_id=' + str(game_id)
	for key in parameters.keys():
		var parameter = parameters[key]
		if parameter == null:
			continue
		if parameter is Array:
			for p in parameter:
				final_url += _compose_param(p, key+"[]")
		else:
			final_url += _compose_param(parameter, key)
	var signature:String = final_url + private_key
	signature = signature.md5_text()
	final_url += '&signature=' + signature
	return final_url

func _on_HTTPRequest_request_completed(result, response_code, headers, response_body) -> void:
	if result != OK:
		emit_signal('gamejolt_request_completed', current_request.type, {"success":false})
	else:
		var body:String = response_body.get_string_from_utf8()
		# Prepare for json parsing
		body = body.replace('"true"',"true")
		body = body.replace('"false"',"false")
		_verbose("Received Response (" + body + ")")
		var json_result = JSON.parse_string(body)
		var response:Dictionary = {'success': false}
		if json_result:
			response = json_result.get('response',{})
		if current_request.type != "/batch/" or raw_batch_requests:
			emit_signal('gamejolt_request_completed',current_request.type,response)
		elif response.has("responses"):
			for k in response["responses"].size():
				if current_request.sub_types.size()>k:
					emit_signal('gamejolt_request_completed',current_request.sub_types[k],response["responses"][k])
	_busy = false
	if !queue.is_empty():
		var request_queued: Request = queue.pop_front()
		_call_gj_api(request_queued.type, request_queued.parameters, request_queued.sub_types)

func _verbose(message:Variant) -> void:
	if verbose: print('[GameJoltAPI] ',message)

func is_busy() -> bool: return _busy ## Returns true if the API has an open request.
func get_username() -> String: return _username_cache ## Returns the username cache.
func get_token() -> String: return _user_token_cache ## Returns the user token cache.
func clear_user(): _username_cache = ""; _user_token_cache = "" ## Clears the username and user token cache.
func time_fetch(): _call_gj_api('/time/',{}) ## Fetches the current time.[br][br]Request type is "/time/"

## Sends multiple [GameJoltAPI.Request]s as a batch request.
## [br]If [param parallel] is true, all sub requests will be handled all at once instead of one at a time.
## If all the requests are fetches and won't change anything, this should be true.
func batch_request(requests:Array[Request], parallel:bool=false, break_on_error:bool=false):
	var requests_url:Array = []
	var sub_types:Array = []
	for request in requests:
		sub_types.push_back(request.type)
		requests_url.push_back(_compose_url(request.type, request.parameters,true))
	_call_gj_api('/batch/',{requests = requests, parallel = parallel, break_on_error = break_on_error}, sub_types)

#region USERS
## Attempts to automatically authenticate the user with the URL of Web builds. Does nothing in regular builds.
## [br]When debugging, you can add this to the URL: [codeblock lang=text]?gjapi_username=<yourusername>&gjapi_token=<yourtoken>[/codeblock] Request type is "/users/auth/"
func user_auto_auth() -> void:
	if OS.has_feature("web"):
		JavaScriptBridge.eval('var urlParams = new URLSearchParams(window.location.search);',true)
		var tmp = JavaScriptBridge.eval('urlParams.get("gjapi_username")', true)
		if tmp is String:
			_username_cache = tmp
			tmp = JavaScriptBridge.eval('urlParams.get("gjapi_token")', true)
			if tmp is String:
				_user_token_cache = tmp
				_call_gj_api('/users/auth/')

## Attempts to authenticate a user with the given [param username] and [param token].
## [br][b]NOTE[/b]: The username and token cache are updated here, not after authentication.
## [br][br]Request type is "/users/auth/"
func user_auth(username:String, token:String) -> void:
	_username_cache = username; _user_token_cache = token;
	_call_gj_api('/users/auth/')
## Fetches the info of the [param username] or user [param id].
## [br][br][b]Returns[/b]: [url=https://gamejolt.com/game-api/doc/users/fetch]GameJolt Docs[/url].
## [br][br]Request type is "/users/"
func user_fetch(username:String, id:int=0) -> void:
	_call_gj_api('/users/', {user_token = null, username = username, user_id = id})

## Fetches the currently cached user's friends list. Returns as an array of dictionaries: [code]{"friends": [{"friend_id": integer}, ...]}[/code]
## [br][br]Request type is "/friends/"
func user_friends_fetch() -> void:
	_call_gj_api('/friends/')
#endregion

#region SESSIONS
func session_open(): ## Opens a session ([/i]User must be authenticated[/i]).[br][br]Request type is "/sessions/open/"
	_call_gj_api('/sessions/open/')

func session_ping(): ## Pings an active session ([/i]User must be authenticated[/i]).[br][br]Request type is "/sessions/ping/"
	_call_gj_api('/sessions/ping/')

func session_close(): ## Closes the active session ([/i]User must be authenticated[/i]).[br][br]Request type is "/sessions/close/"
	_call_gj_api('/sessions/close/')

func session_check(): ## Checks for an active session ([/i]User must be authenticated[/i]).[br][br]Request type is "/sessions/check/"
	_call_gj_api('/sessions/check/')
#endregion

#region SCORES
## Fetches an [Array] of scores for the cached user, a [param guest] or [param global]ly.[br]Leave [param guest] empty if you want global or cached user.
## [br][br][b]Returns[/b]: [url=https://gamejolt.com/game-api/doc/scores/fetch]GameJolt Docs[/url].
## [br][br]Request type is "/scores/"
func scores_fetch(global=false, guest:String = "", limit:int=10, table_id=null, better_than=null, worse_than=null) -> void:
	var parameters = {limit = limit, table_id = table_id, better_than = better_than, worse_than = worse_than}
	if !guest.is_empty(): parameters["guest"] = guest
	_call_gj_api('/scores/', parameters, [], !global and guest.is_empty())

## Attempts to add a score to the cached user or guest.[br]Leave [param guest] empty to use the cached user ([/i]User must be authenticated[/i]).
## [br][br]Request type is "/scores/add/"
func scores_add(score_string, sort_number, guest:String="", table_id=null) -> void:
	_call_gj_api('/scores/add/',
		{score=score_string, sort=sort_number, guest=guest, table_id=table_id}, [], guest.is_empty())

## Returns the rank of a score on a table (scoreboard). [br][param table_id] of -1 means it will use the primary table
## [br][br][b]Returns [code]rank[/code] ([int])[/b]: The rank of the score on the scoreboard.
## [br][br]Request type is "/scores/get-rank/"
func scores_fetch_rank(sort:int, table_id:int=-1) -> void:
	_call_gj_api('/scores/get-rank/', {sort = sort, table_id = table_id if table_id > -1 else null})

func scores_fetch_tables() -> void: ## Fetches the list of high-score tables (scoreboards). [br][br]Request type is "/scores/tables/"
	_call_gj_api('/scores/tables/', {})
#endregion

#region TROPHIES
## Fetches trophies.[br][br]If you want a specific trophy or set of trophies, pass them through [param trophy_ids] as '123456,135792' each separated by commas.
## [br]If you want only achieved or unachieved trophies, pass [param achieved] as 'true' or 'false'.
## [br][br][b]Returns[/b]: [url=https://gamejolt.com/game-api/doc/trophies/fetch]GameJolt Docs[/url].
## [br][br]Request type is "/trophies/"
func trophy_fetch(trophy_ids:String, achieved:String = '') -> void:
	if _username_cache != null: _call_gj_api('/trophies/',
		{achieved = achieved, trophy_id = trophy_ids})

## Unlocks the trophy with id [param trophy_id] for the authenticated user. [i]User must be authenticated[/i]
## [br][br]Request type is "/trophies/add-achieved/"
func trophy_add_achieved(trophy_id:int) -> void:
	if _username_cache != null: _call_gj_api('/trophies/add-achieved/',
		{trophy_id = trophy_id})

## Removes the trophy with id [param trophy_id] from the authenticated user. [i]User must be authenticated[/i][br][b]NOTE[/b]: Only for debugging.
## [br][br]Request type is "/trophies/remove-achieved/"
func trophy_remove_achieved(trophy_id:int) -> void:
	if _username_cache != null: _call_gj_api('/trophies/remove-achieved/',
		{trophy_id = trophy_id})
#endregion

#region DATA STORE
## Fetches data from the key of the user or of global.[br][br]Request type is "/data-store/".
func data_fetch(key:String, global:bool=true) -> void:
	_call_gj_api('/data-store/', {key = key}, [], !global)

## Stores data in the cloud of the user or of global.[br]
## If [param global] is true, this is a global key. If false, it will use the cached user.
## [br][br]Request type is "/data-store/set/"
func data_set(key:String, data:String, global:bool=true) -> void:
	_call_gj_api('/data-store/set/', {key = key, data = data}, [], !global)

## Updates data in the [param key] of the user or of global using an operation.[br]
## [param operation] can be "append", "prepend", "divide", "multiply", "add", or "subtract".[br]For example: [code]data_update("login_count", "add", 1, false)[/code] will increase the cached user's "login_count" key by 1.
## [br][br]Request type is "/data-store/update/"
func data_update(key:String, operation:String, value:Variant, global:bool=true) -> void:
	_call_gj_api('/data-store/update/', {key = key, operation = operation, value = value}, [], !global)

## Removes the [param key] of the user or of global.[br][br]Request type is "/data-store/remove/".
func data_remove(key:String, global:bool=true) -> void:
	_call_gj_api('/data-store/remove/', {key = key}, [], !global)

## Fetches all the keys of either a user's or the global's cloud.[br]Setting the pattern will only fetch keys with applicable key names.
## [br][br]Request type is "/data-store/get-keys/".
func data_fetch_keys(pattern=null, global:bool=true) -> void:
	_call_gj_api('/data-store/get-keys/', {pattern = pattern}, [], !global)
#endregion
