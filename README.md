# What?

This is the obfuscation prototype targeting .NET programs of the paper [Prob­fu­s­ca­ti­on: An Ob­fu­s­ca­ti­on Ap­proach using Pro­ba­bi­lis­tic Con­trol Flows](https://www.syssec.rub.de/research/publications/probfuscation/), published at the Conference on Detection of Intrusions and Malware & Vulnerability Assessment (DIMVA), Donostia-San Sebastian, Spain, July 2016.


# How to use?

The prototype uses [CCI Metadata](https://ccimetadata.codeplex.com/) as a framework. If you want to use it, please use the Visual Studio Solution to compile the "Probfuscator" project. This creates a console application which can obfuscate arbitrary methods.

In order to obfuscate a function, you can for example execute the following command:

```bash

Probfuscator.exe e:\MD5.exe e:\MD5_obfu.exe MD5 MD5 CalculateMD5Value 4 4 3 10 10 10 10 10 2

```

The arguments are the following:

* inputBinary - Path to the binary that should be obfuscated.
* outputBinary - Path and name of the binary that is created by the obfuscator.
* namespace - The namespace that target class resides in (case sensitive).
* class - The class that target method resides in (case sensitive).
* method - The method that should be obfuscated (case sensitive).
* depth - The depth of the obfuscation graph.
* dimension - The dimension of the obfuscation graph.
* numberValidPaths - Number of vpaths through the obfuscation graph.
* duplicateBasicBlockWeight - A weighting value that is used in the random decision if a basic block is duplicated or not.
* duplicateBasicBlockCorrectionValue - This correction value is added to the weighting value after each obfuscation iteration in order to ensure the termination of the process.
* stateChangeWeight - A weighting value that is used in the random decision if code to change the vpath is added or not.
* stateChangeCorrectionValue - This correction value is added to the weighting value after each obfuscation iteration in order to ensure the termination of the process.
* insertOpaquePredicateWeight - A weighting value that is used in the random decision if an opaque predicate is added or not.
* seed - Seed of the PRNG that is used by the prototype.