import { Component, EventEmitter, Input, Output, OnInit } from '@angular/core';
import { UserProfile } from 'src/app/_models/userProfile';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';

@Component({
  selector: 'app-apiview-agent',
  templateUrl: './apiview-agent.component.html',
  styleUrls: ['./apiview-agent.component.scss']
})
export class APIViewAgentComponent implements OnInit {
  @Input() review: Review | undefined;
  @Input() activeAPIRevision: APIRevision | undefined;
  @Input() userProfile: UserProfile | undefined;
  @Input() preferredApprovers: string[] = [];
  
  @Output() requestReviewEmitter: EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() closeAgentEmitter: EventEmitter<boolean> = new EventEmitter<boolean>();

  // Tab Management
  activeTab: 'summary' | 'generated' | 'analysis' | 'chat' = 'summary';

  // Summary Configuration
  selectedSummaryType: 'apiContent' | 'comments' = 'apiContent';
  selectedSummaryStyle: 'concise' | 'verbose' = 'concise';
  
  // New Summary Configuration (for generating additional summaries)
  newSummaryType: 'apiContent' | 'comments' = 'apiContent';
  newSummaryStyle: 'concise' | 'verbose' = 'concise';
  
  // Summary State
  isGeneratingSummary: boolean = false;
  generatedSummary: string = '';
  summaryGeneratedTime: Date | undefined;

  ngOnInit() {
    // Set default selections
    this.selectedSummaryType = 'apiContent';
    this.selectedSummaryStyle = 'concise';
    // Initialize new summary selections
    this.newSummaryType = 'apiContent';
    this.newSummaryStyle = 'concise';
  }

  setActiveTab(tab: 'summary' | 'generated' | 'analysis' | 'chat') {
    this.activeTab = tab;
  }

  generateSummary() {
    if (this.isGeneratingSummary) return;
    
    this.isGeneratingSummary = true;
    this.generatedSummary = '';
    
    // Simulate API call for generating summary
    setTimeout(() => {
      this.generatedSummary = this.getMockSummary();
      this.summaryGeneratedTime = new Date();
      this.isGeneratingSummary = false;
    }, 2000);
  }

  generateAdditionalSummary() {
    if (this.isGeneratingSummary) return;
    
    // Update the selected types to the new ones
    this.selectedSummaryType = this.newSummaryType;
    this.selectedSummaryStyle = this.newSummaryStyle;
    
    // Generate the new summary
    this.generateSummary();
  }

  getSummaryTitle(): string {
    const typeMap = {
      'apiContent': 'API Content Summary',
      'comments': 'Comments Summary'
    };
    return typeMap[this.selectedSummaryType];
  }

  getMockSummary(): string {
    const summaries = {
      'apiContent': {
        'concise': `**Key Changes:**
• Added 3 new public methods to DemoClient class
• Introduced optional parameter support in existing methods
• No breaking changes detected
• API follows Azure SDK guidelines`,
        
        'verbose': `**Comprehensive API Analysis:**

**New Additions:**
• DemoClient.GetResourceAsync() - Retrieves resources with optional filtering
• DemoClient.ListResourcesAsync() - Lists all available resources with pagination
• DemoClient.DeleteResourceAsync() - Safely removes resources with validation

**Modifications:**
• Updated existing methods to support optional parameters for better flexibility
• Enhanced error handling across all public methods
• Improved documentation comments for better IntelliSense support

**Compliance:**
• All new methods follow Azure SDK design guidelines
• Proper async/await patterns implemented
• Consistent naming conventions maintained
• No breaking changes introduced

**Recommendations:**
• Consider adding bulk operations for better performance
• Documentation could benefit from more usage examples`
      },
      
      'comments': {
        'concise': `**Review Feedback Summary:**
• 2 active conversations requiring attention
• 1 suggestion for parameter naming improvement
• All critical issues have been addressed
• Overall positive review feedback`,
        
        'verbose': `**Detailed Comment Analysis:**

**Active Discussions:**
• Parameter naming in GetResourceAsync method - suggestion to use 'resourceId' instead of 'id'
• Error handling approach - discussion about specific exception types vs generic exceptions

**Resolved Items:**
• Initial concerns about breaking changes have been addressed
• Documentation improvements have been implemented
• Code style issues have been fixed

**Community Feedback:**
• Positive feedback on API design approach
• Appreciation for maintaining backward compatibility
• Suggestions for additional convenience methods

**Action Items:**
• Consider renaming parameter for better clarity
• Review exception handling strategy
• Add usage examples to documentation`
      }
    };

    return summaries[this.selectedSummaryType][this.selectedSummaryStyle];
  }

  copySummary() {
    if (this.generatedSummary) {
      navigator.clipboard.writeText(this.generatedSummary).then(() => {
        // You could add a toast notification here
        console.log('Summary copied to clipboard');
      });
    }
  }

  clearSummary() {
    this.generatedSummary = '';
    this.summaryGeneratedTime = undefined;
    this.isGeneratingSummary = false;
  }

  closeAgent() {
    this.closeAgentEmitter.emit(true);
  }
}
