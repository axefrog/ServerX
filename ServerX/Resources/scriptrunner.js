function __getJsonArgs(args) {
	var jsonArgs = [];
	for(var i=0; i<args.length; i++)
		jsonArgs.push(JSON.stringify(args[i]));
	return JSON.stringify(jsonArgs);
}
function __processJsonResponse(response) {
	response = JSON.parse(response);
	if(response.JsonCallError)
		throw response.JsonCallError;
	return response;
}
var Extensions = {};
