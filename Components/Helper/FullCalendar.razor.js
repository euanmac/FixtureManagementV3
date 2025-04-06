export function onUpdate() {
    console.log('Updated');
}

export function onLoad() {
        console.log('Loaded');
        var calendarEl = document.getElementById('calendar');
        var initialView = calendarEl.getAttribute('initialview')
        var initialDate = calendarEl.getAttribute('initialdate')

        var calendar = new FullCalendar.Calendar(calendarEl, {
          initialView: initialView,
          initialDate: initialDate,
          allDaySlot: false,
          slotDuration: '00:30:00', /* If we want to split day time each 15minutes */
          slotMinTime: '09:00:00', /* calendar start Timing */
          slotMaxTime: '23:00:00',  /* calendar end Timing */
          expandRows: true,
          schedulerLicenseKey: 'CC-Attribution-NonCommercial-NoDerivatives',

          slotLabelFormat: {
              hour: '2-digit',
              minute: '2-digit',
              meridiem: false
          },
          datesAboveResources: true,
          resources: [
            {
                "id": "a37dbd57-1722-4f2a-91a3-af141bb2029c",
                "title": "Stadium",
                "displayOrder": 1
            },
            {
                "id": "8f55b18e-368e-4dad-9a56-1df8c2731702",
                "title": "Pitch 2",
                "displayOrder": 2
            },
            {
                "id": "7ca8cb22-4b34-4c53-aeae-d15eceeecaa1",
                "title": "Pitch 3",
                "displayOrder": 3
            },
            {
                "id": "92c2dd82-38c9-4b1a-aacf-e9f207b9dd7a",
                "title": "Pitch 4",
                "displayOrder": 4
            },
            {
                "id": "9e365bfe-ddc2-4296-83df-b278a846207f",
                "title": "Large 3G",
                "displayOrder": 5
            },
            {
                "id": "b89084d2-1095-428c-bc52-885f2b207c1b",
                "title": "Meadow",
                "displayOrder": 6
            },
            {
                "id": "ff4b08c8-dc9a-4f4a-9ec1-bada343145b9",        
                "title": "Small Astro",
                "displayOrder": 0
            }],
            resourceOrder: 'displayOrder',
            events: 'api/event',
            eventDrop: async function (info) {
                alert(JSON.stringify(info.event, null, 4));

                let ev = info.event;
                let eventData = `{"id": "${ev.id}", "title": "${ev.title}","start": "${ev.start.toJSON()}","end": "${ev.end.toJSON()}","resourceId": "${ev.getResources()[0].id}"}`
                let reqOptions = {
                    method: 'PUT',
                    headers: {
                        'Content-Type': 'application/json'
                    },

                }
                
                const response = await fetch('/api/Event/' + info.event.id, {
                    method: 'PUT', // *GET, POST, PUT, DELETE, etc.
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: eventData // body data type must match "Content-Type" header
                });
            
                if (!response.ok) {
                    const message = `An error has occurred: ${response.statusText}`;
                    throw new Error(message);
                }

                // $.ajax({
                //     url: '/api/Event/' + info.event.id,
                //     type: 'PUT',
                //     dataType: 'application/json',
                //     contentType: 'application/json',
                //     data: eventData,
                //     success: function (data, textStatus, xhr) {
                //         console.log(data);
                //     },
                //     error: function (xhr, textStatus, errorThrown) {
                //         info.revert();
                //     }
                // });
            },

        });

        calendar.render();
  }