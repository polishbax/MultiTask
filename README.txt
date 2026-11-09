TinyTask Safe Clone — Visual Studio

Projekt C# / .NET 8 WinForms.
Nie używa zewnętrznych bibliotek ani instalatora/bundlera.
Kod korzysta wyłącznie z WinAPI do przechwytywania i odtwarzania wejścia.

Wymagania:
- Visual Studio 2022
- workload: .NET desktop development
- .NET 8 SDK

Uruchomienie:
1. Rozpakuj ZIP.
2. Otwórz TinyTaskSafeClone.csproj w Visual Studio.
3. F5.
4. Kliknij „Nagrywaj”, wykonaj akcje, kliknij „Stop”.
5. „Odtwórz” powtarza makro. F8 zatrzymuje.

Projekt nie zawiera gotowego EXE ani instalatora.


Poprawiona wersja:
- nagrywa także ruch myszy (WM_MOUSEMOVE), więc odtwarzanie nie teleportuje kursora po prostej,
- zachowuje osobno mouse down i mouse up, więc kliknięcia nie są zamieniane w długie przytrzymanie,
- zapisuje i odtwarza key_down/key_up,
- poprawia kolejność zdarzeń dzięki bezpośredniemu zapisowi z hooka,
- centruje napis „TinyTask Safe Clone”.


Dodatkowa poprawka ruchu myszy:
- WM_MOUSEMOVE jest próbkowany w krótkich odstępach zamiast zapisywania każdego duplikatu,
- ścieżka kursora jest odtwarzana punkt po punkcie,
- przy każdym kliknięciu kursor jest ustawiany dokładnie na nagraną pozycję,
- przed mouse-down jest krótka pauza 70 ms, aby aplikacja zdążyła zarejestrować pozycję,
- po Stop program pokazuje liczbę ruchów, kliknięć i zdarzeń klawiatury.


v3 Diagnostic + Pretty UI: centered controls and diagnostic counters for mouse moves, clicks, keyboard events and scroll, plus last click coordinates and timing.

v4 UI Refresh:
- po kliknięciu, które otwiera menu, opcjonalnie wykonuje mikro-ruch myszy +1/-1 px;
- opcja „Odśwież UI po kliknięciu” jest domyślnie włączona;
- można ją wyłączyć dla zwykłych aplikacji, jeśli nie jest potrzebna;
- nie obchodzi zabezpieczeń ani anti-cheat.


v5 Timer:
- F8 jest globalnym skrótem STOP, także gdy aktywny jest Roblox lub inna aplikacja.
- Powtórzenia zwiększone do 9,999,999,999,999,999,999,999,999,999.
- Limit czasu (h): 0 = bez limitu; np. 10.00 = maksymalnie 10 godzin.
- Odtwarzanie kończy się przy pierwszym spełnionym limicie: liczba powtórzeń albo czas.
- Zachowano v4 „Odśwież UI po kliknięciu”.

v6 Movement + Logs:
- rejestry/diagnostyka są zwijane i rozwijane przyciskiem;
- aplikacja startuje kompaktowo, bez nachodzenia paneli;
- dodano „Odśwież wejście ruchu po klawiszach”;
- ruch jest odtwarzany z normalnych zdarzeń klawiatury zapisanych w makrze;
- brak obchodzenia zabezpieczeń lub anti-cheat.

v7 Keyboard + UI:
- rejestr klawiatury ignoruje powtarzające się WM_KEYDOWN generowane podczas przytrzymania klawisza;
- zapisywany jest scan code klawisza i flaga extended;
- odtwarzanie klawiatury używa KEYEVENTF_SCANCODE zamiast samego virtual-key code;
- poprawiono układ panelu opcji, aby elementy nie nachodziły na siebie;
- checkbox „Klawiatura SCAN CODE (Roblox)” jest domyślnie włączony.
- nie stosuje obchodzenia zabezpieczeń/anti-cheat.


v7 FIXED:
- poprawiono błąd CS0126: hook klawiatury zawsze zwraca IntPtr z CallNextHookEx;
- poprawiono błąd CS0103: stan przytrzymanych klawiszy należy do LowLevelHook i jest resetowany przez ResetHeldKeys().


v7 FIXED2:
- poprawiono ostatni błąd CS0103: StartRecording używa pola LowLevelHook o nazwie „hook”.


v8 Pretty UI:
- nazwa aplikacji: Multitask;
- Output jest domyślnie zamknięty;
- mniejszy interfejs;
- Powtórzenia + Zapisz + Wczytaj są w jednym wyśrodkowanym rzędzie;
- „Limit czasu (godz.)” ma osobny opis oraz „0 = bez limitu”;
- „Roblox Movement” zastępuje nazwę SCAN CODE;
- checkbox odświeżania opisano jako „Odśwież UI po kliknięciu (Roblox Buy)”.

v9 UI REDESIGN:
- całkowicie zmieniony układ ustawień;
- Powtórzenia / Zapisz / Wczytaj są jednym wyśrodkowanym wierszem;
- Limit czasu jest osobnym wierszem z jednostką „godz.” oraz „0 = bez limitu”;
- opcje Buy i Roblox Movement są w trzecim wierszu;
- Output jest zamknięty przy starcie;
- kompaktowe okno 620x360.
