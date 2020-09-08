const io = require('socket.io')({
	transports: ['websocket'],
});

const port = process.env.PORT || 4567;

io.attach(port);

io.on('connection', function(socket){
	console.log("Client has connected!");
	socket.on('beep', function(){
		console.log("beep");
		socket.emit('boop');
	});
})

console.log("Running beep server on port " + port);