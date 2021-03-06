#!/usr/bin/env python3
"""
Raspberry Pi Animatroller Expander
"""


#Import Modules
import os, sys, argparse, math, time, threading, random
import pygame
from pygame.locals import *

from pythonosc import dispatcher
from pythonosc import osc_server
from pythonosc import osc_message_builder
from pythonosc import udp_client
from os import listdir
from os.path import isfile, join
import pifacecommon
import pifacedigitalio as pif
from serial import Serial
from serial import serialutil
import logging
import uuid

logging.basicConfig(filename='/var/log/animatroller.log', filemode='w', level=logging.DEBUG, format='%(asctime)s %(levelname)-8s %(message)s')

# define a Handler which writes INFO messages or higher to the sys.stderr
console = logging.StreamHandler()
console.setLevel(logging.DEBUG)
# set a format which is simpler for console use
formatter = logging.Formatter('%(asctime)s %(levelname)-8s %(message)s')
# tell the handler to use this format
console.setFormatter(formatter)
# add the handler to the root logger
logging.getLogger('').addHandler(console)

logging.info('Starting')

if not pygame.mixer: logging.warning('Warning, sound disabled')

ser = None
  
pfd = None
try:
    pif.init()
    pfd = pif.PiFaceDigital()
except pif.NoPiFaceDigitalDetectedError:
    logging.warning('No PiFace card detected')
    pass

soundPath = 'christmassounds'
bgPath = os.path.join(soundPath, 'bg')
soundFXdict = {}
client = udp_client
last_fx_chn = None
last_fx_snd = None
bg_volume = 0.5
bg_files = []
bg_playing = 0
last_input_values = [0] * 8
input_mute = [None] * 8
input_lock = threading.Lock()
last_bg_index = -1

#functions to create our resources
def load_fx(name):

    sound = soundFXdict.get(name.lower())

    if sound is not None:
        return sound

    fullname = os.path.join(soundPath + '/fx', name)
    try:
        logging.info('Loading ' + fullname)
        sound = pygame.mixer.Sound(fullname)
    except:
        pass
        logging.warning('Cannot load sound: ' + name)
        return None

    soundFXdict[name.lower()] = sound
    return sound


def play_next_bg_track():
    global bg_playing, last_bg_index
    logging.info('Next background track')

    while True:	
        index = random.randint(0, len(bg_files) - 1)
        if len(bg_files) > 1 and last_bg_index == index:
            continue
        break
    last_bg_index = index

    logging.info('File = ' + bg_files[index])

    pygame.mixer.music.load(os.path.join(bgPath, bg_files[index]))
    pygame.mixer.music.set_volume(bg_volume)
    pygame.mixer.music.play()
    bg_playing = 1

    msg = osc_message_builder.OscMessageBuilder(address = "/audio/bg/start")
    msg.add_arg(uuid.uuid1().hex)
    msg.add_arg(os.path.splitext(bg_files[index])[0])
    msg = msg.build()
    client.send(msg)

def cue_track(file):
    logging.info('Cue track ' + file)
    pygame.mixer.music.load(os.path.join(soundPath + '/trk', file))
    pygame.mixer.music.set_volume(1.0)


def decode_motor_command(cmd):
    logging.info('Decode motor command: ' + cmd)
    cmds = cmd.split(',')

    motor_chn = 0
    motor_pos = None
    
    if len(cmds) >= 2:
        motor_chn = int(cmds[0])
        
        if cmds[1] == 'X':
            logging.info('Motor {0} failed!'.format(motor_chn))
            motor_pos = 'FAIL'
            
        elif cmds[1].startswith('S'):
            motor_pos = cmds[1]
            pos = int(cmds[1][1:])
            logging.info('Motor {0} start moving, currently in position {1}'.format(motor_chn, pos))

        elif cmds[1].startswith('E'):
            motor_pos = cmds[1]
            pos = int(cmds[1][1:])
            logging.info('Motor {0} done moving, currently in position {1}'.format(motor_chn, pos))
            
        else:
            pos = int(cmds[1])
            logging.info('Motor {0} moving, currently in position {1}'.format(motor_chn, pos))

    if motor_pos is not None:
        motormsg = osc_message_builder.OscMessageBuilder(address = "/motor/feedback")
        motormsg.add_arg(uuid.uuid1().hex)
        motormsg.add_arg(motor_chn)
        motormsg.add_arg(motor_pos)
        motormsg = motormsg.build()
        client.send(motormsg)


def main():
    global bg_files, last_input_values, input_mute
    """this function is called when the program starts.
    it initializes everything it needs, then runs in
    a loop until the function returns."""

    #Initialize Everything
    os.environ["SDL_VIDEODRIVER"] = "dummy"
    pygame.mixer.pre_init(frequency=44100, size=-16, channels=2, buffer=2048)
    pygame.init()
    screen = pygame.display.set_mode((80, 25))
    random.seed()

    pygame.mixer.music.set_endevent(pygame.constants.USEREVENT)

    # Find all background tracks
    bg_files = [ f for f in listdir(bgPath) if isfile(join(bgPath, f)) ]

    logging.info('BG files = {0}'.format(len(bg_files)))

    pfd_listener = None
    if pfd is not None:
        pfd_listener = pif.InputEventListener()

        for i in range(8):
            inputValue = pif.digital_read(i)
            send_input_msg(i, inputValue)
            last_input_values[i] = inputValue

            pfd_listener.register(i, pif.IODIR_ON, input_callback)
            pfd_listener.register(i, pif.IODIR_OFF, input_callback)
            
        pfd_listener.activate()

    logging.info('Ready!')
    initmsg = osc_message_builder.OscMessageBuilder(address = "/init")
    initmsg.add_arg(uuid.uuid1().hex)
    initmsg = initmsg.build()
    client.send(initmsg)

    # auto-start BG music
    #play_next_bg_track()

    running = 1

    try:
        if ser is not None:
            ser.write("!!\r".encode('utf-8'))

        while running:
            for event in pygame.event.get(): # User did something
                if event.type == pygame.QUIT: # If user clicked close
                    running = 0
                if event.type == pygame.constants.USEREVENT:
                    # This event is triggered when the song stops playing
                    logging.info ('Music ended')
                    if bg_playing:
                        play_next_bg_track()
                    else:
                        send_track_done()                    

            if ser is not None:
                serline = ser.readline(timeout=0.3)
                if serline != '' and serline.startswith('!IOX:0,'):
                    serline = serline[7:]
                    if len(serline) < 1:
                        continue
                    if serline[0] == '#':
                        logging.info ('Serial: ACK')
                    elif serline[0:2] == 'M,':
                        decode_motor_command(serline[2:].rstrip())
                    else:
                        logging.debug('Serial data: ' + serline)
                else:
                    if serline != '':
                        logging.debug(serline)
            else:
                time.sleep(0.1)

            if pfd is not None:
                now = time.time()
                for i in range(8):
                    if input_mute[i] is not None and (now - input_mute[i]) >= 0.1:
                        input_lock.acquire()
                        input_mute[i] = None
                        inputValue = pif.digital_read(i)
                        if last_input_values[i] != inputValue:                        
                            logging.info('input {0} reset in main to {1}'.format(i, inputValue))
                            update_input(i, inputValue)
                        input_lock.release()


    except KeyboardInterrupt:
        logging.info('Aborting')
        pass

    if pfd is not None:
        pfd_listener.deactivate()
        
    logging.info('Done')


def send_track_done():
    msg = osc_message_builder.OscMessageBuilder(address = "/audio/trk/done")
    msg.add_arg(uuid.uuid1().hex)
    msg = msg.build()
    client.send(msg)


def send_input_msg(channel, button_value):
    logging.info('Input value {0} on channel {1}'.format(button_value, channel))
    buttonmsg = osc_message_builder.OscMessageBuilder(address = "/input")
    buttonmsg.add_arg(uuid.uuid1().hex)
    buttonmsg.add_arg(channel)
    buttonmsg.add_arg(button_value)
    buttonmsg = buttonmsg.build()
    client.send(buttonmsg)


def update_input(pin, value):
    global last_input_values, input_mute
    input_mute[pin] = time.time()
    last_input_values[pin] = value
    send_input_msg(pin, value)


def input_callback(event):
    global last_input_values, input_mute

    pin = event.pin_num
    logging.info('Input_callback {0} value {1}'.format(pin, 1 - event.direction))

    if input_mute[pin] is not None and (time.time() - input_mute[pin]) < 0.1:
        logging.info('muted')
        return

    input_lock.acquire()
    input_mute[pin] = None
    
#    inputValue = pif.digital_read(pin)
    
#    if last_input_values[pin] != inputValue:
    update_input(pin, 1 - event.direction)
    input_lock.release()


def osc_init(unused_addr, args = None):
    logging.info('Animatroller running')


def osc_motor(unused_addr, chn, target, speed, timeout):
    logging.info('Motor command: chn: {0}  target: {1}  speed: {2}  timeout: {3}'.format(chn, target, speed, timeout))
    output = '!M,{0},{1},{2},{3}\r'.format(chn, target, speed, timeout)

    logging.info('Output: ' + output)
    ser.write(output.encode('utf-8'))


def osc_playFx(unused_addr, file, leftvol = -1, rightvol = -1):
    global last_fx_snd, last_fx_chn

    logging.info('Play FX ' + file)
    fx_sound = load_fx(file + '.wav')
    if fx_sound is not None:
        last_fx_snd = fx_sound
        if last_fx_chn is not None:
            last_fx_chn.stop()
        last_fx_chn = fx_sound.play()
        
        leftvol = float(leftvol)
        rightvol = float(rightvol)
        
        if rightvol >= 0 and leftvol >= 0:
            last_fx_chn.set_volume(leftvol, rightvol)
        elif leftvol >= 0:
            last_fx_chn.set_volume(leftvol)


def osc_playNewFx(unused_addr, file, leftvol = -1, rightvol = -1):
    global last_fx_snd, last_fx_chn

    logging.info('Play New FX ' + file)
    fx_sound = load_fx(file + '.wav')
    if fx_sound is not None:
        last_fx_snd = fx_sound
        last_fx_chn = fx_sound.play()
        
        leftvol = float(leftvol)
        rightvol = float(rightvol)
        
        if rightvol >= 0 and leftvol >= 0:
            last_fx_chn.set_volume(leftvol, rightvol)
        elif leftvol >= 0:
            last_fx_chn.set_volume(leftvol)


def osc_cueFx(unused_addr, args):
    global last_fx_snd, last_fx_chn

    logging.info('Cue FX ' + args)
    fx_sound = load_fx(args + '.wav')
    if fx_sound is not None:
        last_fx_snd = fx_sound
        fx_sound.stop()
        last_fx_chn = None


def osc_test(unused_addr, num):
    logging.info('Test {0}'.format(num))


def osc_pauseFx(unused_addr):
    logging.info('Pause FX')
    if last_fx_chn is not None:
        last_fx_chn.pause()


def osc_resumeFx(unused_addr):
    global last_fx_chn
    logging.info('Resume FX')
    if last_fx_chn is not None:
        last_fx_chn.unpause()
    elif last_fx_snd is not None:
        last_fx_chn = last_fx_snd.play()

        
def osc_bgVolume(unused_addr, volume):
    global bg_volume
    logging.info('Background volume {0}'.format(volume))
    bg_volume = float(volume)
    pygame.mixer.music.set_volume(bg_volume)


def osc_bgPlay(unused_addr):
    global bg_playing
    if pygame.mixer.music.get_busy():
        logging.info('Background resume')
        pygame.mixer.music.unpause()
    else:
        logging.info('Background play')
        play_next_bg_track()
    bg_playing = 1

    
def osc_trkPlay(unused_addr, file):
    global bg_playing
    cue_track(file + '.wav')
    pygame.mixer.music.play()
    bg_playing = 0
    

def osc_trkCue(unused_addr, file):
    global bg_playing
    cue_track(file + '.wav')
    bg_playing = 0


def osc_trkResume(unused_addr):
    pygame.mixer.music.play()


def osc_bgPause(unused_addr):
    logging.info('Background pause')
    pygame.mixer.music.pause()


def osc_bgNext(unused_addr):
    logging.info('Background next')
    play_next_bg_track()


def osc_output(unused_addr, channel, value):
    if pfd is not None:
        logging.info('Output {0} set to {1}'.format(channel, value))
        pfd.output_pins[channel].value = value
    else:
        logging.warning('No PiFace card')


class EnhancedSerial(Serial):
    def __init__(self, *args, **kwargs):
        #ensure that a reasonable timeout is set
        timeout = kwargs.get('timeout',0.1)
        if timeout < 0.01: timeout = 0.1
        kwargs['timeout'] = timeout
        Serial.__init__(self, *args, **kwargs)
        self.buf = ''
        
    def readline(self, maxsize=None, timeout=1):
        """maxsize is ignored, timeout in seconds is the max time that is way for a complete line"""
        tries = 0
        while 1:
            self.buf += self.read(512).decode('utf-8') 
            pos = self.buf.find('\r')
            if pos >= 0:
                line, self.buf = self.buf[:pos+1], self.buf[pos+1:]
                return line
            tries += 1
            if tries * self.timeout > timeout:
                break
        line, self.buf = self.buf, ''
        return line

    def readlines(self, sizehint=None, timeout=1):
        """read all lines that are available. abort after timout
        when no more data arrives."""
        lines = []
        while 1:
            line = self.readline(timeout=timeout)
            if line:
                lines.append(line)
            if not line or line[-1:] != '\r':
                break
        return lines


#this calls the 'main' function when this script is executed
if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument("--ip",
        default="0.0.0.0", help="The ip to listen on")
    parser.add_argument("--port",
        type=int, default=5005, help="The port to listen on")
    parser.add_argument("--serverip",
        default="127.0.0.1", help="The server ip to send messages to")
    parser.add_argument("--serverport",
        type=int, default=3333, help="The server port to send messages to")
    parser.add_argument("--serialport", default="", help="Serial port to connect to")
    parser.add_argument("--bgpath",
        default="bg", help="The background sound sub folder")
    args = parser.parse_args()

    dispatcher = dispatcher.Dispatcher()
    dispatcher.map("/init", osc_init)
    dispatcher.map("/test", osc_test)

    dispatcher.map("/audio/fx/play", osc_playFx)
    dispatcher.map("/audio/fx/playnew", osc_playNewFx)
    dispatcher.map("/audio/fx/cue", osc_cueFx)
    dispatcher.map("/audio/fx/pause", osc_pauseFx)
    dispatcher.map("/audio/fx/resume", osc_resumeFx)
    dispatcher.map("/audio/bg/volume", osc_bgVolume)
    dispatcher.map("/audio/bg/play", osc_bgPlay)
    dispatcher.map("/audio/bg/pause", osc_bgPause)
    dispatcher.map("/audio/bg/next", osc_bgNext)

    dispatcher.map("/audio/trk/cue", osc_trkCue)
    dispatcher.map("/audio/trk/play", osc_trkPlay)
    dispatcher.map("/audio/trk/pause", osc_bgPause)
    dispatcher.map("/audio/trk/resume", osc_trkResume)
    
    dispatcher.map("/output", osc_output)
    dispatcher.map("/motor/exec", osc_motor)

    if args.serialport != "":
        sys.stdout.write("Serial port /dev/" + args.serialport + "\n")
        ser = EnhancedSerial("/dev/" + args.serialport, 38400, timeout=0.5)

    bgPath = os.path.join(soundPath, args.bgpath)
    logging.info("bgPath {0}".format(bgPath))
	
    server = osc_server.ThreadingOSCUDPServer(
        (args.ip, args.port), dispatcher)
    logging.info("Serving on {}".format(server.server_address))
    server_thread = threading.Thread(target=server.serve_forever)
    server_thread.start()

    client = udp_client.UDPClient(args.serverip, args.serverport)

    main()  
    pif.deinit()
    pygame.quit()
    server.shutdown()
    if ser is not None:
        ser.close()
    logging.info('Goodbye')
