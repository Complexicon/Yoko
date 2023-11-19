This Repository contains the Source of Yoko.\
This Tool is an all-in-one build-system/debugger for Spigot Plugins!

## how does it work?
easy. we've had tools like vscode, maven and the redhat java extension for vscode for years now. but it doesn't integrate nicely with spigot plugins - this is where Yoko comes in!

with it you can easily manage your plugin settings, minecraft version, debugging and more.
it nicely and quietly generates the correct configs for vscode, downloads and manages your maven dependencies, manages your different spigot versions in a central cache and last but not least: it does support debugging your plugin at runtime and hotreload on code change aswell!

## Usage

Download the latest release into an empty directory and open your console

to create a project type `yoko init`\
to rehydrate a cloned repo or to clean your repo type `yoko hydrate`\
to test without debugger type `yoko test`\
to build the plugin type `yoko build`

upon entering these commands you will be asked if you want to install yoko globally into your path.

## the project.json file
in this file your plugin parameters are set. for example maven dependencies or your minecraft version.\
after changes to this file you need to rehydrate your project in order to download all necessary dependencies. 

## the .yoko folder
the .yoko folder in your project contains your development server aswell as compiled java files (.class files) for makefile like behavior which only recompiles changed sources.\
it also contains your most recent "plugin.jar" a.k.a. your plugin

yoko will also create a ".yoko" folder in your userprofile directory to cache minecraft servers, jdks, dependencies, etc

## currently under construction
- automatic jdk detection (only works on windows currently. hold tight my fellow linux and mac users!)
- maven dependency system half baked
- weird edge cases of minecraft version incompatabilities (ex. 1.8 not downloadable anymore from mojang servers)

## Thanks!
a big thank you goes out to the dev team of papermc, spigot and craftbukkit for providing such an awesome platform for creating minecraft plugins
