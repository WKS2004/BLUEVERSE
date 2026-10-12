import 'package:flutter/material.dart';

import '../data/models/planner_models.dart';
import '../data/services/planner_api_service.dart';

class PlannerDraftPicker extends StatefulWidget {
  const PlannerDraftPicker({super.key, required this.api});
  final PlannerApiService api;
  @override
  State<PlannerDraftPicker> createState() => _PlannerDraftPickerState();
}

class _PlannerDraftPickerState extends State<PlannerDraftPicker> {
  int _page = 1;
  late Future<List<PlannerItinerary>> _future;
  @override
  void initState() {
    super.initState();
    _load();
  }

  void _load() {
    _future = widget.api.itineraries(page: _page, showLoadingScreen: false);
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Choose a draft')),
    body: FutureBuilder<List<PlannerItinerary>>(
      future: _future,
      builder: (context, snapshot) {
        final loading = snapshot.connectionState != ConnectionState.done;
        final rows = snapshot.data ?? const <PlannerItinerary>[];
        final drafts = rows.where((trip) => trip.status == 'DRAFT');
        return ListView(
          padding: const EdgeInsets.all(20),
          children: [
            const Text(
              'Add this experience to one of your private drafts. The latest plan details are checked before saving.',
            ),
            const SizedBox(height: 20),
            if (loading)
              const Text('Opening your drafts…')
            else if (snapshot.hasError)
              TextButton(
                onPressed: () => setState(_load),
                child: const Text('Drafts unavailable · Try again'),
              )
            else ...[
              if (drafts.isEmpty) const Text('No drafts on this page.'),
              for (final trip in drafts)
                Card(
                  child: ListTile(
                    title: Text(trip.title),
                    subtitle: Text('${trip.items.length} experiences'),
                    onTap: () => Navigator.pop(context, trip.id),
                  ),
                ),
            ],
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                TextButton(
                  onPressed: loading || _page == 1
                      ? null
                      : () => setState(() {
                          _page--;
                          _load();
                        }),
                  child: const Text('Previous'),
                ),
                Text('Page $_page'),
                TextButton(
                  onPressed: loading || snapshot.hasError || rows.length < 20
                      ? null
                      : () => setState(() {
                          _page++;
                          _load();
                        }),
                  child: const Text('Next'),
                ),
              ],
            ),
          ],
        );
      },
    ),
  );
}
