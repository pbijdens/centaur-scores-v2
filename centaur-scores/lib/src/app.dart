import 'package:centaur_scores/src/features/shell/app_shell.dart';
import 'package:centaur_scores/src/mycustomscrollbehavior.dart';
import 'package:centaur_scores/src/navigationservice.dart';
import 'package:centaur_scores/src/repository/repository.dart';
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

/// The Widget that configures your application.
class MyApp extends StatelessWidget {
  const MyApp({
    super.key,
  });

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<void>(
      future: initialize(),
      builder: (context, snapshot) {
        return ListenableBuilder(
          listenable: MatchRepository(),
          builder: (BuildContext context, Widget? child) {
            return MaterialApp(
              navigatorKey: NavigationService.navigatorKey,
              scrollBehavior: MyCustomScrollBehavior(),
              debugShowCheckedModeBanner: false,
              restorationScopeId: 'app',
              localizationsDelegates: const [
                GlobalMaterialLocalizations.delegate,
                GlobalWidgetsLocalizations.delegate,
                GlobalCupertinoLocalizations.delegate,
              ],
              supportedLocales: const [
                Locale('en', ''),
                Locale('nl', ''),
              ],
              locale: Locale(MatchRepository().language.toLowerCase(), ''),
              title: 'Centaur Scores',
              theme: ThemeData(),
              darkTheme: ThemeData.dark(),
              themeMode: ThemeMode.light,
              home: snapshot.connectionState == ConnectionState.done
                  ? const AppShell()
                  : const Scaffold(body: Center(child: CircularProgressIndicator())),
            );
          },
        );
      },
    );
  }

  Future<void> initialize() async {
    debugPrint("Initializing MyApp");
    await MatchRepository().initialize();
  }
}
