{{Motor driver for H Bridge driven motors; for instance L298N based driver circuits, based on code by Jev Kuznetsov and the code from AN001 - propeller counters

┌────────────────────────────────────────────┐
│ PWMMotor Driver 0.8                        │
│ Author: Rick Price (rprice@price-mail.com) │             
│ Copyright (c) <2009> <Rick Price           │             
│ See end of file for terms of use.          │              
└────────────────────────────────────────────┘

 date  :  11 May 2009

 usage

 OBJ
        pwm : PWMMotorDriver

  ....

  pwm.Start(outputEnablePin,driveForwardPin,driveForwardInversePin,Frequency) ' Start PWM on cog with a base frequency of *frequency*
  pwm.SetDuty( duty)            ' set duty in % -100 goes full backward, 0 brakes, 100 goes full forward       
  pwm.Halt                      ' Brake motor
  pwm.Stop                      ' Brake motor for 1s and then stop cog

The output directions are set based on the duty cycle (-100%,0,100%), and the enable pin is pulsed to achieve PWM speed control.

When the duty cycle is set to zero, both sides of the H bridge are set to ground and the enable line is turned on.

}}

VAR
  long  cogon, cog
  long sDriveForwardPin
  long sDriveForwardInversePin
  long sDuty                     ' order important (the variables are read from memory in this order)  
  long sOutputEnablePinOut 
  long sCtraVal
  long sPeriod
  long sCurrentDuty
  

PUB Start(outputEnablePin,driveForwardPin,driveForwardInversePin,Frequency) : okay

  sOutputEnablePinOut := outputEnablePin
  sDriveForwardPin:=driveForwardPin
  sDriveForwardInversePin:=DriveForwardInversePin

  dira[outputEnablePin]~~ ' make output pin *output*
  dira[driveForwardPin]~~ ' Make drive forward pin output
  dira[driveForwardInversePin]~~ ' Make drive forward inverse pin output

  longfill(@sDuty, 0, 4)       
  Halt
  sOutputEnablePinOut := |< outputEnablePin

  sCtraVal :=  %00100 << 26 + outputEnablePin
  SetFrequency(Frequency)

  okay := cogon := (cog := cognew(@entry,@sDuty)) > 0    
  
PUB Stop
  ' Stop object - frees a cog

  if cogon~
    Halt
    waitcnt(clkfreq+cnt)                        ' Wait 1 second for motor to stop and quit
    cogstop(cog)
  longfill(@sDuty, 0, 4) 

PUB Halt
  ' Brake motor (ground both sides of motor continously)
  outa[sDriveForwardPin]:=0
  outa[sDriveForwardInversePin]:=0
  _SetDuty(100)                                 ' Keep outputs on to brake motor
  sCurrentDuty := 0

PUB Coast
  _SetDuty(0)                                 ' Keep outputs on to brake motor
  sCurrentDuty := 0

PUB SetDuty(percent)
' Set duty cycle, 0 halts motor; -1 <-> -100 goes backward; 1 <-> 100 goes forward.
  if (percent == 0)
     Halt
     return
  elseif (percent < 0)
     if (percent < -100)
       percent := -100
     sCurrentDuty := percent
     percent := ||percent
     Backward
  elseif (percent > 0)
     if (percent > 100)
       percent := 100
     sCurrentDuty := percent
     Forward

  _SetDuty(percent)

PUB GetDuty
  Result := sCurrentDuty

PRI Forward
  outa[sDriveForwardPin]:=1
  outa[sDriveForwardInversePin]:=0

PRI Backward
  outa[sDriveForwardPin]:=0
  outa[sDriveForwardInversePin]:=1

PRI _SetDuty(percent)
  sDuty :=percent*sPeriod/100
   
PRI SetFrequency(baseFrequency)
' Set basic frequency for PWM in Hertz
   sPeriod := CLKFREQ/baseFrequency

DAT
'assembly cog which updates the PWM cycle on APIN
'for audio PWM, fundamental freq which must be out of auditory range (period < 50µS)
        org

entry   mov     t1,par                'get first parameter
        rdlong  value, t1
         
        add     t1,#4                 
        rdlong   pinOut, t1
        or       dira, pinOut         ' set pinOut to output      

        add     t1, #4
        rdlong  ctraval, t1
        mov ctra, ctraval              'establish counter A mode and APIN

        add     t1, #4
        rdlong  period, t1


        mov frqa, #1                   'set counter to increment 1 each cycle

        mov time, cnt                  'record current time
        add time, period               'establish next period

:loop   rdlong value, par              'get an up to date pulse width
        waitcnt time, period           'wait until next period
        neg phsa, value                'back up phsa so that it  trips "value" cycles from now
        jmp #:loop                     'loop for next cycle



period  res 1                    
time    res 1
value   res 1
t1      res 1
pinOut  res 1
ctraval res 1


{{
┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                                   TERMS OF USE: MIT License                                                  │                                                            
├──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation    │ 
│files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy,    │
│modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software│
│is furnished to do so, subject to the following conditions:                                                                   │
│                                                                                                                              │
│The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.│
│                                                                                                                              │
│THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE          │
│WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR         │
│COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,   │
│ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.                         │
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
}}    