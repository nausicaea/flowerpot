# The Pot

This is a simulation of potted plants for Unity. The underlying procedural
algorithm is based on Lindenmayer Systems (i.e. L-Systems). I based a lot of
the design on work by [Allen
Pike](http://www.allenpike.com/modeling-plants-with-l-systems/).

Currently, the defaults are configured to draw the L-System characterised by
the axiom `X` and the rule `X=F[-XF][+XF][lXF][rXF],F[-FX][+FX][lFX][rFX]`.
Additional nice-looking examples are:

* Axiom `F` and rule `F=F[-F][lF]F[+F][rF]F,F[lF][rF]F[-F][+F]F`
* Axiom `X` and rule `X=Fl[[X]+X]rF[+FX]-X,F+[[X]rX]-F[lFX]+X`

The `PlantGenerator` class exposes additional variables that allow the
modification of the plant base radius, the degree of rotation for every
rotation operation, etc. refer to the documentation within the code.
